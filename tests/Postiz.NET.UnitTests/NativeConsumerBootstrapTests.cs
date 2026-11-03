using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Postiz;
using Postiz.Appliance;
using Xunit;

namespace Postiz.NET.UnitTests;

// Deterministic transport fixtures only. No native owner, engine, DB or release proof.
public sealed class NativeConsumerBootstrapTests
{
    private static readonly string Purpose = new('p', 32);
    private static readonly string Control = new('c', 32);
    private static readonly string Key = new('k', 48);
    private const string Org = "hm-8e92cf7cfaf319223fd619c88951a2eae011b64ad8e9554da1a3f3d75eac29ce";
    private const string User = "sm.8e92cf7cfaf319223fd619c88951a2eae011b64ad8e9554da1a3f3d75eac29ce";
    private const string Integration = "happym-sim-eb50b1aaa08f63cfcd1e969913c1bb1096697af9";
    private static NativeConsumerBinding Binding => new("client-a", "environment-test", "deployment-test", "installation-a");
    private static NativeConsumerBootstrapRequest Input(bool existing = false) =>
        new(Binding, new("deployment-test", "test", "group-a", "pharmacy-a"), "request-a", existing);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delivers_key_only_to_borrowed_sink_and_returns_public_receipt(bool requireExisting)
    {
        var calls = 0;
        using var handler = new Stub(request =>
        {
            calls++;
            Assert.Equal("/api/internal/happym/appliance/native-consumer/bootstrap", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer " + Control, request.Headers.GetValues("Authorization").Single());
            Assert.Equal("client-native", request.Headers.GetValues("X-HappyM-Client-Id").Single());
            Assert.Equal(Purpose, request.Headers.GetValues("X-HappyM-Native-Fixture-Authorization").Single());
            Assert.False(request.Headers.Contains("X-HappyM-Organization-Id"));
            var body = JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!;
            Assert.Equal(requireExisting, body["requireExisting"]!.GetValue<bool>());
            Assert.Equal(4, body.AsObject().Count);
            return Response(Envelope());
        });
        using var http = Http(handler);
        var sink = new Sink();
        var receipt = await Client(http).BootstrapNativeConsumerAsync(Input(requireExisting), Purpose, sink);
        Assert.Equal(Org, receipt.OrganizationId);
        Assert.Equal(User, receipt.UserId);
        Assert.Equal(1, calls);
        Assert.Equal(1, sink.Calls);
        Assert.True(sink.Matched);
        Assert.All(sink.Borrowed.ToArray(), c => Assert.Equal('\0', c));
        Assert.DoesNotContain(Key, receipt.ToString());
        Assert.DoesNotContain("apiKey", JsonSerializer.Serialize(receipt), StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(NativeConsumerBootstrapReceipt).GetProperty("ApiKey"));
    }

    [Theory]
    [InlineData("organizationId")]
    [InlineData("userId")]
    [InlineData("requestId")]
    [InlineData("contract")]
    [InlineData("binding")]
    [InlineData("consumer")]
    [InlineData("extra")]
    [InlineData("control-key")]
    [InlineData("purpose-key")]
    public async Task Rejects_wrong_or_extended_response_before_key_sink(string mutation)
    {
        var envelope = Envelope();
        if (mutation == "control-key") envelope["apiKey"] = Control;
        else if (mutation == "purpose-key") envelope["apiKey"] = Purpose;
        else if (mutation == "binding") envelope["binding"]!["installationId"] = "other";
        else if (mutation == "consumer") envelope["consumer"]!["pharmacyId"] = "other";
        else envelope[mutation] = "wrong";
        using var handler = new Stub(_ => Response(envelope));
        using var http = Http(handler);
        var sink = new Sink();
        var error = await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() =>
            Client(http).BootstrapNativeConsumerAsync(Input(), Purpose, sink));
        Assert.Equal(0, sink.Calls);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public async Task Never_retries_unknown_write_or_reads_secret_error_body()
    {
        var calls = 0;
        using var handler = new Stub(_ =>
        {
            calls++;
            return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(Key + Purpose + Control) };
        });
        using var http = Http(handler);
        var error = await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() =>
            Client(http).BootstrapNativeConsumerAsync(Input(), Purpose, new Sink()));
        Assert.Equal(1, calls);
        Assert.Equal("native_consumer_outcome_unknown", error.Message);
        Assert.Null(error.InnerException);
        Assert.Empty(error.Data);
        Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public async Task Sink_failure_after_a_possible_secret_commit_is_unknown_and_sanitized()
    {
        using var handler = new Stub(_ => Response(Envelope()));
        using var http = Http(handler);
        var sink = new Sink { Fail = true };
        var error = await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() =>
            Client(http).BootstrapNativeConsumerAsync(Input(), Purpose, sink));
        Assert.Equal(1, sink.Calls);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(Key, error.ToString());
        Assert.All(sink.Borrowed.ToArray(), c => Assert.Equal('\0', c));
    }

    [Fact]
    public async Task Wrong_environment_deployment_purpose_and_cancel_fail_before_transport()
    {
        var calls = 0;
        using var handler = new Stub(_ => { calls++; return Response(Envelope()); });
        using var http = Http(handler);
        var client = Client(http);
        var input = Input();
        await Assert.ThrowsAsync<ArgumentException>(() => client.BootstrapNativeConsumerAsync(
            input with { Consumer = input.Consumer with { Environment = "prod" } }, Purpose, new Sink()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.BootstrapNativeConsumerAsync(
            input with { Consumer = input.Consumer with { DeploymentId = "other" } }, Purpose, new Sink()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.BootstrapNativeConsumerAsync(input, Control, new Sink()));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.BootstrapNativeConsumerAsync(input, Purpose, new Sink(), cancel.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Existing_fixture_route_uses_derived_id_and_keeps_provider_distinct()
    {
        using var handler = new Stub(request =>
        {
            Assert.Equal("/api/internal/happym/appliance/test-fixture/provision", request.RequestUri!.AbsolutePath);
            return Response(Fixture());
        });
        using var http = Http(handler);
        var receipt = await Client(http).ProvisionNativeFixtureAsync(new(Binding, Org, "success"), Purpose);
        Assert.Equal(Integration, receipt.IntegrationId);
        Assert.Equal("happym-test-fixture", receipt.Identifier);
    }

    [Theory]
    [InlineData("integrationId", "other")]
    [InlineData("identifier", "facebook")]
    [InlineData("mode", "LIVE")]
    [InlineData("label", "secret-reflection")]
    public async Task Rejects_fixture_identity_or_mode_mismatch(string field, string value)
    {
        var body = Fixture(); body[field] = value;
        using var handler = new Stub(_ => Response(body));
        using var http = Http(handler);
        await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() =>
            Client(http).ProvisionNativeFixtureAsync(new(Binding, Org, "success"), Purpose));
    }

    private static JsonObject Envelope() => JsonSerializer.SerializeToNode(new {
        contract = PostizNativeConsumerClient.BootstrapContract,
        binding = new { clientId = Binding.ClientId, applicationEnvironmentId = Binding.ApplicationEnvironmentId,
            logicalDeploymentId = Binding.LogicalDeploymentId, installationId = Binding.InstallationId },
        consumer = new { deploymentId = "deployment-test", environment = "test", tenantId = "group-a", pharmacyId = "pharmacy-a" },
        requestId = "request-a", organizationId = Org, userId = User, apiKey = Key
    })!.AsObject();
    private static JsonObject Fixture() => JsonSerializer.SerializeToNode(new {
        contract = SyntheticDeliveryContract.Contract, mode = SyntheticDeliveryContract.Mode,
        integrationId = Integration, identifier = "happym-test-fixture", label = "[SIMULAZIONE] success"
    })!.AsObject();
    private static HttpResponseMessage Response(JsonObject value) => new(HttpStatusCode.Created) {
        Content = new StringContent(value.ToJsonString(), Encoding.UTF8, "application/json") };
    private static HttpClient Http(HttpMessageHandler handler) => new(handler);
    private static PostizNativeConsumerClient Client(HttpClient http) => new(http, new PostizOptions {
        BaseAddress = new("http://127.0.0.1:25427/api/"), InternalClientId = "client-native", InternalClientSecret = Control,
        MaxRetryAttempts = 6, RequestTimeout = TimeSpan.FromSeconds(2)
    });
    private sealed class Sink : IPostizNativeCredentialSink
    {
        public int Calls { get; private set; }
        public bool Matched { get; private set; }
        public bool Fail { get; init; }
        public ReadOnlyMemory<char> Borrowed { get; private set; }
        public ValueTask ReceiveAsync(NativeConsumerBootstrapReceipt receipt, ReadOnlyMemory<char> nativeKey, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++; Borrowed = nativeKey;
            Matched = nativeKey.Span.SequenceEqual(Key.AsSpan()) && receipt.OrganizationId == Org;
            if (Fail) throw new InvalidOperationException(Key);
            return ValueTask.CompletedTask;
        }
    }
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> next) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = next(request); response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }
}
