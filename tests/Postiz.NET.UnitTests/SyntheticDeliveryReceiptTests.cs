using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Postiz;
using Postiz.Appliance;
using Postiz.PrePublishRender;
using Xunit;

namespace Postiz.NET.UnitTests;

// Transport fixtures only; these cases do not qualify an engine or database.
public sealed class SyntheticDeliveryReceiptTests
{
    [Fact]
    public async Task Reads_NotStarted_with_existing_M2M_and_exact_scope_query()
    {
        using var handler = new Stub(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/internal/happym/appliance/test-fixture/receipts", request.RequestUri!.AbsolutePath);
            Assert.Equal("?organizationId=org%3Aa&postId=post.a&occurrenceId=occ-1", request.RequestUri.Query);
            Assert.Equal("Bearer synthetic-internal-secret", request.Headers.GetValues("Authorization").Single());
            Assert.Equal("synthetic-client", request.Headers.GetValues("X-HappyM-Client-Id").Single());
            Assert.False(request.Headers.Contains("X-HappyM-Native-Fixture-Authorization"));
            return Body();
        });
        using var http = Http(handler);
        var result = await Client(http).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1");
        Assert.Equal(SyntheticDeliveryStatuses.NotStarted, result.Status);
        Assert.Null(result.BodyHash);
        Assert.Empty(result.Slides);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task Reads_partial_and_completed_journals_without_real_publication_receipts()
    {
        using var handler = new Stub(_ => Body(SyntheticDeliveryStatuses.Partial));
        using var http = Http(handler);
        var partial = await Client(http).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1");
        Assert.Equal("SimulatedCommitted", Assert.Single(partial.Slides).Status);
        Assert.Equal("ResponseLost", partial.Events[^1].Kind);
        using var completedHandler = new Stub(_ => Body(SyntheticDeliveryStatuses.SimulatedPublished));
        using var completedHttp = Http(completedHandler);
        var completed = await Client(completedHttp).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1");
        Assert.Equal(PrePublishRenderStatuses.SimulatedPublished, completed.Status);
        Assert.NotEqual(PrePublishRenderStatuses.Published, completed.Status);
        Assert.Contains(completed.Events, item => item.Kind == "Completed");
    }

    [Fact]
    public async Task Reads_rejected_attempts_before_any_slide_commit()
    {
        var body = Body();
        body["bodyHash"] = new string('a', 64);
        body["events"] = JsonSerializer.SerializeToNode(new[] {
            new { ordinal = 1, kind = "Attempted" }, new { ordinal = 2, kind = "TransientRejected" } });
        using var handler = new Stub(_ => body);
        using var http = Http(handler);
        var receipt = await Client(http).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1");
        Assert.Equal(SyntheticDeliveryStatuses.NotStarted, receipt.Status);
        Assert.Equal(2, receipt.Events.Count);
    }

    [Theory]
    [InlineData("contract", "different/v1")]
    [InlineData("mode", "LIVE")]
    [InlineData("status", "Published")]
    [InlineData("status", "Unknown")]
    [InlineData("organizationId", "other-org")]
    [InlineData("postId", "other-post")]
    [InlineData("occurrenceId", "other-occurrence")]
    public async Task Rejects_other_contract_mode_status_or_scope(string field, string value)
    {
        var body = Body(); body[field] = value;
        using var handler = new Stub(_ => body);
        using var http = Http(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1"));
    }

    [Theory]
    [InlineData("providerId")]
    [InlineData("releaseUrl")]
    [InlineData("publishReceipt")]
    public async Task Rejects_real_publication_fields_in_the_synthetic_contract(string field)
    {
        var body = Body(SyntheticDeliveryStatuses.SimulatedPublished); body[field] = "synthetic-unexpected";
        using var handler = new Stub(_ => body);
        using var http = Http(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1"));
    }

    [Theory]
    [InlineData("slideStatus")]
    [InlineData("slideIndex")]
    [InlineData("receiptId")]
    [InlineData("eventKind")]
    [InlineData("eventOrdinal")]
    [InlineData("bodyHash")]
    [InlineData("missingCompleted")]
    public async Task Rejects_unknown_or_inconsistent_journal_entries(string mutation)
    {
        var body = Body(SyntheticDeliveryStatuses.SimulatedPublished);
        switch (mutation)
        {
            case "slideStatus": body["slides"]![0]!["status"] = "Published"; break;
            case "slideIndex": body["slides"]![0]!["slideIndex"] = 1; break;
            case "receiptId": body["slides"]![0]!["syntheticReceiptId"] = "provider-receipt"; break;
            case "eventKind": body["events"]![0]!["kind"] = "ProviderPublished"; break;
            case "eventOrdinal": body["events"]![0]!["ordinal"] = 0; break;
            case "bodyHash": body["bodyHash"] = null; break;
            case "missingCompleted": body["events"]![2]!["kind"] = "ResponseLost"; break;
        }
        using var handler = new Stub(_ => body);
        using var http = Http(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1"));
    }

    [Fact]
    public async Task Cancels_and_rejects_invalid_scope_before_transport()
    {
        var calls = 0;
        using var handler = new Stub(_ => { calls++; return Body(); });
        using var http = Http(handler);
        var client = Client(http);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Appliance.GetSyntheticDeliveryReceiptAsync("org:a", "post.a", "occ-1", cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Appliance.GetSyntheticDeliveryReceiptAsync("../other", "post.a", "occ-1"));
        Assert.Equal(0, calls);
    }

    private static JsonObject Body(string status = SyntheticDeliveryStatuses.NotStarted)
    {
        var hasSlides = status != SyntheticDeliveryStatuses.NotStarted;
        return JsonSerializer.SerializeToNode(new {
            contract = SyntheticDeliveryContract.Contract, mode = SyntheticDeliveryContract.Mode, status,
            organizationId = "org:a", integrationId = "happym-sim-a", postId = "post.a", occurrenceId = "occ-1",
            bodyHash = hasSlides ? new string('a', 64) : null,
            slides = hasSlides ? new[] { new { slideIndex = 0, syntheticReceiptId = "synthetic_" + new string('b', 64), status = "SimulatedCommitted" } } : [],
            events = hasSlides ? new[] { new { ordinal = 1, kind = "Attempted" }, new { ordinal = 2, kind = "SlideCommitted" },
                new { ordinal = 3, kind = status == SyntheticDeliveryStatuses.SimulatedPublished ? "Completed" : "ResponseLost" } } : []
        })!.AsObject();
    }

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://postiz.test/api/") };
    private static IPostizClient Client(HttpClient http) => new PostizClient(http, new PostizOptions {
        BaseAddress = http.BaseAddress!, ApiKey = "synthetic-api-key", InternalClientId = "synthetic-client",
        InternalClientSecret = "synthetic-internal-secret", MaxRetryAttempts = 0
    });
    private sealed class Stub(Func<HttpRequestMessage, JsonObject> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(body(request).ToJsonString(), Encoding.UTF8, "application/json")
            });
    }
}
