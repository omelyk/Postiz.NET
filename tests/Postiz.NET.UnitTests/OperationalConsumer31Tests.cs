using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Postiz.Appliance;
using Xunit;

namespace Postiz.NET.UnitTests;

// Actual client-code boundary tests with a deterministic HTTP transport adapter.
// No native caller, database, private deployed credential or runtime qualification.
public sealed class OperationalConsumer31Tests
{
    private static readonly string Control = new('c', 48), Purpose = new('p', 48), Admin = new('a', 48), Key = new('k', 48);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static NativeConsumerBinding Appliance => new("appliance-owner", "appliance-test", "appliance-shared", "appliance-install");
    private static NativeConsumerIdentity30 Consumer => new("client-a", "qa", "deployment-a", "crm");
    private static NativeOperationalRegistrationRequest31 Registration => new("happym.postiz-operational-register31/v1", Appliance,
        Consumer, new('d', 32), new('b', 64), null, "2030-01-01T00:10:00.000Z",
        Enumerable.Range(1, 7).Select(i => new NativeOperationalScopeClaim31("pharmacy-" + i, "group-a", "pharmacy-" + i)).ToArray());
    private static string Operation => PostizNativeConsumerClient.OperationalId31(Appliance, Consumer, Registration.RequestId, Registration.InputSha256);
    private static string Workspace(string id) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] {
        "happym.postiz.consumer-workspace/v2", Consumer.ClientId, Consumer.ApplicationEnvironmentId, Consumer.LogicalDeploymentId,
        Consumer.ServiceId, "test", "group-a", id }))).ToLowerInvariant();
    private static NativeOperationalBootstrapRequest31 Bootstrap => new(PostizNativeConsumerClient.OperationalBootstrapContract31,
        Operation, Appliance, Consumer, new("deployment-a", "test", "group-a", "pharmacy-1"), Registration.RequestId);
    private static NativeAdministrativeCredential31 Authority() => new("happym-system", Admin.AsMemory());
    private static JsonObject Registered() => JsonSerializer.SerializeToNode(new {
        contract = PostizNativeConsumerClient.OperationalRegistrationContract31, operationId = Operation,
        requestId = Registration.RequestId, inputSha256 = Registration.InputSha256, previousOperationId = (string?)null,
        revision = 1, notAfterUtc = Registration.NotAfterUtc, applianceBinding = Appliance, consumerBinding = Consumer,
        administrator = new { organizationId = "happym-system", userId = "native-admin" },
        scopes = Registration.Scopes.Select(x => new { x.PackagePharmacyKey, x.TenantId, x.PharmacyId,
            organizationId = "hm-" + Workspace(x.PharmacyId), userId = "sm." + Workspace(x.PharmacyId) })
    }, Json)!.AsObject();
    private static JsonObject Envelope() => JsonSerializer.SerializeToNode(new {
        contract = PostizNativeConsumerClient.OperationalBootstrapContract31, operationId = Operation,
        applianceBinding = Appliance, consumerBinding = Consumer, consumer = Bootstrap.Consumer, requestId = Registration.RequestId,
        organizationId = "hm-" + Workspace("pharmacy-1"), userId = "sm." + Workspace("pharmacy-1"), apiKey = Key
    }, Json)!.AsObject();

    [Fact]
    public async Task Seven_scopes_use_distinct_admin_headers_and_an_exact_full_server_registration()
    {
        using var h = new Stub(request => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/operational-register31", request.RequestUri!.AbsolutePath);
            Assert.Equal(Admin, request.Headers.GetValues("X-HappyM-Native-System-Authorization31").Single());
            Assert.Equal("happym-system", request.Headers.GetValues("X-HappyM-Native-System-Organization31").Single());
            var body = JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!;
            Assert.Equal(7, body["scopes"]!.AsArray().Count); Assert.DoesNotContain(Admin, body.ToJsonString()); return Response(Registered());
        });
        using var http = Http(h); using var admin = Authority();
        var result = await Client(http).RegisterOperationalConsumer31Async(Registration, Purpose, admin);
        Assert.Equal(7, result.Scopes.Length); Assert.Equal(Operation, result.OperationId); Assert.Equal(1, h.Calls);
        Assert.DoesNotContain(Admin, JsonSerializer.Serialize(result));
    }
    [Theory]
    [InlineData("consumerBinding")]
    [InlineData("administrator")]
    [InlineData("seventhWorkspace")]
    [InlineData("extraCredential")]
    public async Task Foreign_or_extended_registration_is_rejected_without_retry(string fault)
    {
        var body = Registered();
        if (fault == "consumerBinding") body["consumerBinding"]!["clientId"] = "foreign";
        if (fault == "administrator") body["administrator"]!["organizationId"] = "scoped-workspace";
        if (fault == "seventhWorkspace") body["scopes"]![6]!["organizationId"] = "hm-" + new string('0', 64);
        if (fault == "extraCredential") body["apiKey"] = Key;
        using var h = new Stub(_ => Response(body)); using var http = Http(h); using var admin = Authority();
        var error = await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).RegisterOperationalConsumer31Async(Registration, Purpose, admin));
        Assert.Equal(1, h.Calls); Assert.DoesNotContain(Key, error.ToString());
    }
    [Fact]
    public async Task Recovery_existing_workspace_uses_GET_and_borrowed_sink_without_mutating_POST()
    {
        using var h = new Stub(request => { Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content);
            Assert.Contains("/workspace/hm-", request.RequestUri!.AbsolutePath); return Response(Envelope()); });
        using var http = Http(h); using var admin = Authority(); var sink = new Sink();
        var result = await Client(http).BootstrapOperationalConsumer31Async(Bootstrap, Purpose, admin, sink, readExisting: true);
        Assert.Equal(1, h.Calls); Assert.Equal(1, sink.Calls); Assert.All(sink.Borrowed.ToArray(), c => Assert.Equal('\0', c));
        Assert.Null(result.GetType().GetProperty("ApiKey")); Assert.DoesNotContain(Key, JsonSerializer.Serialize(result));
    }
    [Fact]
    public async Task Fresh_workspace_uses_the_separate_POST_contract_with_one_call()
    {
        using var h = new Stub(request => { Assert.Equal(HttpMethod.Post, request.Method); Assert.EndsWith("/operational-bootstrap31", request.RequestUri!.AbsolutePath); return Response(Envelope()); });
        using var http = Http(h); using var admin = Authority(); var sink = new Sink();
        await Client(http).BootstrapOperationalConsumer31Async(Bootstrap, Purpose, admin, sink);
        Assert.Equal(1, h.Calls); Assert.Equal(1, sink.Calls);
    }
    [Fact]
    public async Task Current_SYSTEM_key_cannot_be_delivered_as_a_workspace_key()
    {
        var body = Envelope(); body["apiKey"] = Admin;
        using var h = new Stub(_ => Response(body)); using var http = Http(h); using var admin = Authority(); var sink = new Sink();
        await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).BootstrapOperationalConsumer31Async(Bootstrap, Purpose, admin, sink));
        Assert.Equal(0, sink.Calls); Assert.Equal(1, h.Calls);
    }
    [Fact]
    public async Task Administrative_key_has_no_public_serialized_value_and_disposal_revokes_transport_access()
    {
        using var admin = Authority(); Assert.Equal("{}", JsonSerializer.Serialize(admin)); Assert.DoesNotContain(Admin, admin.ToString());
        admin.Dispose(); using var h = new Stub(_ => Response(Registered())); using var http = Http(h);
        await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).RegisterOperationalConsumer31Async(Registration, Purpose, admin));
        Assert.Equal(0, h.Calls);
    }
    [Fact]
    public void A_shared_default_administrative_header_is_rejected_by_the_transport_constructor()
    {
        using var h = new Stub(_ => Response(Registered())); using var http = Http(h);
        http.DefaultRequestHeaders.Add("X-HappyM-Native-System-Authorization31", Admin);
        Assert.Throws<ArgumentException>(() => Client(http)); Assert.Equal(0, h.Calls);
    }
    [Fact]
    public async Task Existing_registration_is_recognized_using_GET_only()
    {
        using var h = new Stub(request => { Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content);
            Assert.EndsWith("/operational31/" + Operation, request.RequestUri!.AbsolutePath); return Response(Registered()); });
        using var http = Http(h); using var admin = Authority();
        var receipt = await Client(http).ReadOperationalRegistration31Async(Registration, Purpose, admin);
        Assert.Equal(Operation, receipt.OperationId); Assert.Equal(1, h.Calls);
    }
    [Fact]
    public async Task Absent_workspace_observation_is_a_readonly_server_state_not_a_ready_flag()
    {
        var scope = Bootstrap.Consumer;
        using var h = new Stub(request => { Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content);
            return Response(JsonSerializer.SerializeToNode(new { contract = "happym.postiz-operational-workspace-state31/v1",
                operationId = Operation, requestId = Registration.RequestId, organizationId = "hm-" + Workspace("pharmacy-1"),
                userId = "sm." + Workspace("pharmacy-1"), consumer = scope, state = "Absent" }, Json)!.AsObject()); });
        using var http = Http(h); using var admin = Authority();
        var receipt = await Client(http).ObserveOperationalWorkspace31Async(Bootstrap, Purpose, admin);
        Assert.Equal("Absent", receipt.State); Assert.Equal(1, h.Calls);
    }
    [Fact]
    public async Task Cancellation_after_EOF_before_operational_sink_never_delivers_a_credential()
    {
        using var cancellation = new CancellationTokenSource();
        using var h = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(new CancelAtEof(Encoding.UTF8.GetBytes(Envelope().ToJsonString()), cancellation)) });
        using var http = Http(h); using var admin = Authority(); var sink = new Sink();
        await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).BootstrapOperationalConsumer31Async(
            Bootstrap, Purpose, admin, sink, ct: cancellation.Token));
        Assert.Equal(0, sink.Calls); Assert.Equal(1, h.Calls);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_unknown_is_reconciled_by_an_admin_GET_and_never_by_repeating_POST(bool forgedReady)
    {
        var body = JsonSerializer.SerializeToNode(new { contract = "happym.postiz-operational-registration-state31/v1",
            operationId = Operation, state = "Absent" }, Json)!.AsObject();
        if (forgedReady) body["ready"] = true;
        using var h = new Stub(request => { Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content);
            Assert.EndsWith("/operational31/" + Operation + "/status", request.RequestUri!.AbsolutePath);
            var binding = JsonNode.Parse(Convert.FromBase64String(request.Headers.GetValues("X-HappyM-Operational-Binding31").Single()))!;
            Assert.Equal(Consumer.ClientId, binding["consumerBinding"]!["clientId"]!.GetValue<string>());
            Assert.Equal(Appliance.InstallationId, binding["applianceBinding"]!["installationId"]!.GetValue<string>());
            Assert.Equal(Registration.RequestId, binding["requestId"]!.GetValue<string>());
            Assert.Equal(Registration.InputSha256, binding["inputSha256"]!.GetValue<string>());
            Assert.Equal(Admin, request.Headers.GetValues("X-HappyM-Native-System-Authorization31").Single()); return Response(body); });
        using var http = Http(h); using var admin = Authority();
        if (forgedReady) await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).ObserveOperationalRegistration31Async(Registration, Purpose, admin));
        else Assert.Equal("Absent", (await Client(http).ObserveOperationalRegistration31Async(Registration, Purpose, admin)).State);
        Assert.Equal(1, h.Calls);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operational_fixture_mutation_is_scoped_and_carries_current_SYSTEM_authority(bool foreignReceipt)
    {
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] {
            Appliance.ClientId, Appliance.ApplicationEnvironmentId, Appliance.LogicalDeploymentId, Appliance.InstallationId,
            "hm-" + Workspace("pharmacy-1"), "success" }))).ToLowerInvariant();
        using var h = new Stub(request => {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.EndsWith("/operational-fixture31", request.RequestUri!.AbsolutePath);
            Assert.Equal(Admin, request.Headers.GetValues("X-HappyM-Native-System-Authorization31").Single());
            var payload = JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!;
            Assert.Equal(Operation, payload["operationId"]!.GetValue<string>());
            Assert.Equal(Consumer.ClientId, payload["consumerBinding"]!["clientId"]!.GetValue<string>());
            return Response(JsonSerializer.SerializeToNode(new { contract = "happym.synthetic-delivery/v1", mode = "SIMULATION",
                integrationId = foreignReceipt ? "foreign" : "happym-sim-" + digest[..40], identifier = "happym-test-fixture", label = "[SIMULAZIONE] success" })!.AsObject()); });
        using var http = Http(h); using var admin = Authority();
        if (foreignReceipt) await Assert.ThrowsAsync<PostizNativeOutcomeUnknownException>(() => Client(http).ProvisionOperationalFixture31Async(Bootstrap, "success", Purpose, admin));
        else Assert.Equal("happym-sim-" + digest[..40], (await Client(http).ProvisionOperationalFixture31Async(Bootstrap, "success", Purpose, admin)).IntegrationId);
        Assert.Equal(1, h.Calls);
    }
    private sealed class CancelAtEof(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { var count = Read(buffer.Span); if (count == 0) cancellation.Cancel(); return ValueTask.FromResult(count); }
    }
    private static PostizNativeConsumerClient Client(HttpClient http) => new(http, new PostizOptions {
        BaseAddress = new Uri("http://127.0.0.1:25428/api/"), InternalClientId = "control", InternalClientSecret = Control, MaxRetryAttempts = 0 });
    private static HttpClient Http(HttpMessageHandler h) => new(h) { BaseAddress = new Uri("http://127.0.0.1:25428/api/") };
    private static HttpResponseMessage Response(JsonObject body) => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; var response = respond(request); response.RequestMessage = request;
            response.Content.Headers.ContentType ??= new("application/json"); return Task.FromResult(response); }
    }
    private sealed class Sink : IPostizOperationalNativeCredentialSink31
    {
        internal int Calls; internal ReadOnlyMemory<char> Borrowed;
        public ValueTask ReceiveAsync(NativeOperationalBootstrapReceipt31 receipt, ReadOnlyMemory<char> nativeKey, CancellationToken ct)
        { Calls++; Borrowed = nativeKey; Assert.Equal(Key, nativeKey.ToString()); return ValueTask.CompletedTask; }
    }
}
