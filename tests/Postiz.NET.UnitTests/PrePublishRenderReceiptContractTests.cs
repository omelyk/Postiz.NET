using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Postiz;
using Postiz.Appliance;
using Xunit;

namespace Postiz.NET.UnitTests;

// Local HTTP handler only: no engine, database or live transport qualification.
public sealed class PrePublishRenderReceiptContractTests
{
    [Fact]
    public async Task Reads_only_the_occurrence_URI_with_API_key_and_organization_scope()
    {
        var calls = 0;
        using var handler = new Handler(request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/public/v1/prepublish-render/occurrences/occ%3A1/synthetic-receipt", request.RequestUri!.AbsolutePath);
            Assert.Empty(request.RequestUri.Query);
            Assert.Equal("synthetic-api-key", request.Headers.GetValues("Authorization").Single());
            Assert.False(request.Headers.Contains("X-HappyM-Organization-Id"));
            Assert.False(request.Headers.Contains("X-HappyM-Client-Id"));
            Assert.False(request.Headers.Contains("X-HappyM-Native-Fixture-Authorization"));
            Assert.False(request.Headers.Contains("X-HappyM-Native-System-Authorization31"));
            Assert.Null(request.Content);
            return Response(Body());
        });
        using var http = Http(handler);
        var receipt = await Client(http).PrePublishRender.GetSyntheticDeliveryReceiptAsync("occ:1", "post.a", "org:a");
        Assert.Equal(SyntheticDeliveryStatuses.NotStarted, receipt.Status);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(SyntheticDeliveryStatuses.NotStarted, null)]
    [InlineData(SyntheticDeliveryStatuses.Partial, "org:a")]
    [InlineData(SyntheticDeliveryStatuses.SimulatedPublished, null)]
    public async Task Reads_the_current_synthetic_DTO_and_journal(string status, string? configuredOrganizationId)
    {
        using var handler = new Handler(_ => Response(Body(status)));
        using var http = Http(handler);
        SyntheticDeliveryReceipt receipt = await Client(http, organizationId: configuredOrganizationId).PrePublishRender.GetSyntheticDeliveryReceiptAsync("occ:1", "post.a", "org:a");
        Assert.Equal(SyntheticDeliveryContract.Contract, receipt.Contract);
        Assert.Equal(SyntheticDeliveryContract.Mode, receipt.Mode);
        Assert.Equal(status, receipt.Status);
        Assert.DoesNotContain(receipt.Events, item => item.Kind == "ProviderPublished");
        if (status == SyntheticDeliveryStatuses.NotStarted) Assert.Empty(receipt.Slides);
        else Assert.Equal("SimulatedCommitted", Assert.Single(receipt.Slides).Status);
    }

    [Theory]
    [InlineData("organizationId", "other-org")]
    [InlineData("postId", "other-post")]
    [InlineData("occurrenceId", "other-occurrence")]
    [InlineData("contract", "noop/v1")]
    [InlineData("mode", "LIVE")]
    [InlineData("status", "Published")]
    [InlineData("providerId", "real-provider-result")]
    public async Task Rejects_other_scope_noop_and_real_publication_contracts(string field, string value)
    {
        var body = Body(); body[field] = value;
        using var handler = new Handler(_ => Response(body));
        using var http = Http(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).PrePublishRender.GetSyntheticDeliveryReceiptAsync("occ:1", "post.a", "org:a"));
    }

    [Fact]
    public async Task Rejects_an_empty_noop_response()
    {
        using var handler = new Handler(_ => Response(new JsonObject()));
        using var http = Http(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).PrePublishRender.GetSyntheticDeliveryReceiptAsync("occ:1", "post.a", "org:a"));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Preserves_HTTP_failure_and_never_falls_back_to_the_fixture(int status)
    {
        var calls = 0;
        using var handler = new Handler(request =>
        {
            calls++;
            Assert.Contains("/public/v1/prepublish-render/occurrences/", request.RequestUri!.AbsolutePath);
            return new((HttpStatusCode)status) { Content = new StringContent("{\"code\":\"synthetic_receipt_rejected\"}", Encoding.UTF8, "application/json") };
        });
        using var http = Http(handler);
        var error = await Assert.ThrowsAsync<PostizApiException>(() => Client(http).PrePublishRender.GetSyntheticDeliveryReceiptAsync("occ:1", "post.a", "org:a"));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null, null, "org:a", "occ:1", "post.a")]
    [InlineData("synthetic-api-key", null, null, "occ:1", "post.a")]
    [InlineData("synthetic-api-key", "other-org", "org:a", "occ:1", "post.a")]
    [InlineData("synthetic-api-key", null, "org:a", "../other", "post.a")]
    [InlineData("synthetic-api-key", null, "org:a", "occ:1", " ")]
    public async Task Rejects_missing_API_scope_or_invalid_identifiers_before_transport(
        string? apiKey, string? organizationId, string? expectedOrganizationId, string occurrenceId, string expectedPostId)
    {
        var calls = 0;
        using var handler = new Handler(_ => { calls++; return Response(Body()); });
        using var http = Http(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => Client(http, apiKey, organizationId).PrePublishRender.GetSyntheticDeliveryReceiptAsync(occurrenceId, expectedPostId, expectedOrganizationId!));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Cancellation_precedes_transport()
    {
        var calls = 0;
        using var handler = new Handler(_ => { calls++; return Response(Body()); });
        using var http = Http(handler);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).PrePublishRender.GetSyntheticDeliveryReceiptAsync("occ:1", "post.a", "org:a", cancellation.Token));
        Assert.Equal(0, calls);
    }

    private static JsonObject Body(string status = SyntheticDeliveryStatuses.NotStarted)
    {
        var journal = status != SyntheticDeliveryStatuses.NotStarted;
        return JsonSerializer.SerializeToNode(new {
            contract = SyntheticDeliveryContract.Contract, mode = SyntheticDeliveryContract.Mode, status,
            organizationId = "org:a", integrationId = "happym-sim-a", postId = "post.a", occurrenceId = "occ:1",
            bodyHash = journal ? new string('a', 64) : null,
            slides = journal ? new[] { new { slideIndex = 0, syntheticReceiptId = "synthetic_" + new string('b', 64), status = "SimulatedCommitted" } } : [],
            events = journal ? new[] { new { ordinal = 1, kind = "Attempted" }, new { ordinal = 2, kind = "SlideCommitted" },
                new { ordinal = 3, kind = status == SyntheticDeliveryStatuses.SimulatedPublished ? "Completed" : "ResponseLost" } } : []
        })!.AsObject();
    }

    private static HttpResponseMessage Response(JsonObject body) => new(HttpStatusCode.OK) {
        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new("https://postiz.test/api/") };
    private static IPostizClient Client(HttpClient http, string? apiKey = "synthetic-api-key", string? organizationId = null) => new PostizClient(http, new PostizOptions {
        BaseAddress = http.BaseAddress!, ApiKey = apiKey, OrganizationId = organizationId,
        InternalClientId = "synthetic-control-client", InternalClientSecret = "synthetic-control-secret", MaxRetryAttempts = 0 });
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
