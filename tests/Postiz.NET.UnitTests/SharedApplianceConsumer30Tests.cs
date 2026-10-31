using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Postiz.Appliance;
using Xunit;

namespace Postiz.NET.UnitTests;

// New deterministic transport cases, never native/SQL/image/HA qualification.
public sealed class SharedApplianceConsumer30Tests
{
    private const string Id = "430a80272fc6119f80661f491e368c863b98044a4bf11e3ca807465f10f2d5d6";
    private const string Workspace = "882ee5fa8b3535e8bb253fc9b4285e8098fb44b1a56b0dee81ad69dc9ddf93f1";
    private static readonly string Request = new('d', 32), Control = new('c', 48), Purpose = new('p', 48), Key = new('k', 48);
    private static NativeConsumerBinding Appliance => new("appliance-owner", "appliance-test", "appliance-shared", "appliance-install");
    private static NativeConsumerIdentity30 Consumer => new("client-a", "qa", "deployment-a", "crm");
    private static NativeConsumerScope Scope => new("deployment-a", "test", "group-a", "pharmacy-a");
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static NativeSharedConsumerBootstrapRequest30 Input => new(PostizNativeConsumerClient.SharedBootstrapContract30,
        Id, Appliance, Consumer, Scope, Request, false);
    private static JsonObject Envelope() => JsonSerializer.SerializeToNode(new
    {
        contract = PostizNativeConsumerClient.SharedBootstrapContract30, admissionId = Id,
        applianceBinding = Appliance, consumerBinding = Consumer, consumer = Scope, requestId = Request,
        organizationId = "hm-" + Workspace, userId = "sm." + Workspace, apiKey = Key
    }, Json)!.AsObject();
    private static JsonObject Admission() => JsonSerializer.SerializeToNode(new
    {
        contract = PostizNativeConsumerClient.AdmissionContract30, admissionId = Id, requestId = Request,
        applianceBinding = Appliance, consumerBinding = Consumer,
        scopes = new[] { new { tenantId = "group-a", pharmacyId = "pharmacy-a", organizationId = "hm-" + Workspace, userId = "sm." + Workspace } }
    }, Json)!.AsObject();

    [Fact]
    public void Two_distinct_consumers_share_one_appliance_with_distinct_admission_ids()
    {
        Assert.Equal(Id, PostizNativeConsumerClient.AdmissionId30(Appliance, Consumer));
        var other = Consumer with { ClientId = "client-b", LogicalDeploymentId = "deployment-b" };
        Assert.NotEqual(Id, PostizNativeConsumerClient.AdmissionId30(Appliance, other));
        Assert.Equal("appliance-shared", Appliance.LogicalDeploymentId);
    }

    [Fact]
    public async Task Enrollment_sends_both_identities_and_requires_the_exact_server_readback()
    {
        using var handler = new Stub(request =>
        {
            Assert.Equal("/api/internal/happym/appliance/native-consumer/enroll30", request.RequestUri!.AbsolutePath);
            var body = JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!;
            Assert.Equal("appliance-owner", body["applianceBinding"]!["clientId"]!.GetValue<string>());
            Assert.Equal("client-a", body["consumerBinding"]!["clientId"]!.GetValue<string>());
            Assert.Equal(Purpose, request.Headers.GetValues("X-HappyM-Native-Fixture-Authorization").Single());
            return Response(Admission());
        });
        using var http = Http(handler);
        var result = await Client(http).EnrollNativeConsumer30Async(new("happym.native-consumer-enroll/v1", Appliance,
            Consumer, Request, false, [new("group-a", "pharmacy-a")]), Purpose);
        Assert.Equal(Id, result.AdmissionId); Assert.Equal(Consumer, result.ConsumerBinding); Assert.Equal(Appliance, result.ApplianceBinding);
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Foreign_consumer_cannot_adopt_an_existing_admission_DTO()
    {
        using var doc = JsonDocument.Parse(Admission().ToJsonString());
        Assert.Throws<JsonException>(() => PostizNativeConsumerClient.ParseAdmission30(doc.RootElement, Appliance,
            Consumer with { ClientId = "foreign" }, Request, [new("group-a", "pharmacy-a")]));
    }

    [Fact]
    public async Task Bootstrap_delivers_only_the_matched_consumers_key_to_the_borrowed_sink()
    {
        using var handler = new Stub(request =>
        {
            Assert.Equal("/api/internal/happym/appliance/native-consumer/bootstrap30", request.RequestUri!.AbsolutePath);
            return Response(Envelope());
        });
        using var http = Http(handler); var sink = new Sink();
        var result = await Client(http).BootstrapSharedNativeConsumer30Async(Input, Purpose, sink);
        Assert.Equal(1, sink.Calls); Assert.Equal(Consumer, result.ConsumerBinding); Assert.Equal(Appliance, result.ApplianceBinding);
        Assert.All(sink.Borrowed.ToArray(), c => Assert.Equal('\0', c));
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(result)); Assert.Null(result.GetType().GetProperty("ApiKey"));
    }

    [Theory]
    [InlineData("admissionId")]
    [InlineData("consumerBinding")]
    [InlineData("applianceBinding")]
    [InlineData("consumer")]
    [InlineData("organizationId")]
    [InlineData("contract")]
    [InlineData("extra")]
    public async Task Wrong_or_extended_bootstrap_response_is_rejected_before_the_key_sink(string field)
    {
        var body = Envelope();
        if (field == "consumerBinding") body[field]!["clientId"] = "foreign";
        else if (field == "applianceBinding") body[field]!["clientId"] = "foreign";
        else if (field == "consumer") body[field]!["pharmacyId"] = "foreign";
        else body[field] = "wrong";
        using var handler = new Stub(_ => Response(body)); using var http = Http(handler); var sink = new Sink();
        var error = await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).BootstrapSharedNativeConsumer30Async(Input, Purpose, sink));
        Assert.Equal(0, sink.Calls); Assert.Null(error.InnerException); Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public async Task Unknown_post_is_never_retried_and_error_body_is_not_exposed()
    {
        var calls = 0;
        using var handler = new Stub(_ => { calls++; return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(Key) }; });
        using var http = Http(handler);
        var error = await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).BootstrapSharedNativeConsumer30Async(Input, Purpose, new Sink()));
        Assert.Equal(1, calls); Assert.Null(error.InnerException); Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public async Task Cancellation_between_response_EOF_and_private_sink_never_delivers_the_key()
    {
        using var cancel = new CancellationTokenSource(); var calls = 0;
        using var handler = new Stub(_ =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StreamContent(new CancelAtEof(Encoding.UTF8.GetBytes(Envelope().ToJsonString()), cancel)) };
            response.Content.Headers.ContentType = new("application/json");
            return response;
        });
        using var http = Http(handler); var sink = new Sink();
        await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() =>
            Client(http).BootstrapSharedNativeConsumer30Async(Input, Purpose, sink, cancel.Token));
        Assert.True(cancel.IsCancellationRequested); Assert.Equal(0, sink.Calls); Assert.Equal(1, calls);
    }

    private sealed class CancelAtEof(byte[] bytes, CancellationTokenSource cancel) : MemoryStream(bytes, writable: false)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var count = await base.ReadAsync(buffer, ct);
            if (count == 0) cancel.Cancel(); // IO itself returned EOF; cancel before the decoder's sink callback.
            return count;
        }
    }

    private sealed class Sink : IPostizSharedNativeCredentialSink30
    {
        internal int Calls; internal ReadOnlyMemory<char> Borrowed;
        public ValueTask ReceiveAsync(NativeSharedConsumerBootstrapReceipt30 receipt, ReadOnlyMemory<char> key, CancellationToken ct)
        {
            Calls++; Borrowed = key; Assert.Equal(Id, receipt.AdmissionId); Assert.Equal(Consumer, receipt.ConsumerBinding);
            Assert.Equal(Key, new string(key.Span)); return ValueTask.CompletedTask;
        }
    }
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { var response = respond(request); response.RequestMessage = request; return Task.FromResult(response); }
    }
    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://127.0.0.1:25428/api/") };
    private static PostizNativeConsumerClient Client(HttpClient http) => new(http, new PostizOptions
    { BaseAddress = http.BaseAddress!, InternalClientId = "control", InternalClientSecret = Control, MaxRetryAttempts = 0 });
    private static HttpResponseMessage Response(JsonObject body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
