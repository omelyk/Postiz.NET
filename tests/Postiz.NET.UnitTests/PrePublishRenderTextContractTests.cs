using System.Net;
using System.Text;
using System.Text.Json;
using Postiz;
using Postiz.PrePublishRender;
using Xunit;

namespace Postiz.NET.UnitTests;

public sealed class PrePublishRenderTextContractTests
{
    private static readonly RenderCorrelation Correlation = new("crm-1", "snapshot-1", "group-1");
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-08-26T08:00:00+00:00");
    private const string TextGolden = "sha256-f34d397f4d3b7d5fc787623725cfef6aafcc4bed131a11fb0cbf0c7f5dd545cd";
    private const string MediaGolden = "sha256-bf381ad74fc1c555acfc9658df00d8a5732c57ed4bcc202e25d6ca5330695835";

    [Fact]
    public async Task Explicit_text_only_uses_existing_attach_route_without_media_upload()
    {
        var calls = 0;
        using var handler = new Handler(request =>
        {
            calls++;
            Assert.Equal("/public/v1/prepublish-render/occurrences/occ-1/attach-rendered", request.RequestUri!.AbsolutePath);
            Assert.Equal("attach-text-v3", request.Headers.GetValues("Idempotency-Key").Single());
            Assert.Equal("pharmacy-1", request.Headers.GetValues("X-HappyM-Organization-Id").Single());
            using var json = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var target = json.RootElement.GetProperty("targets")[0];
            Assert.Equal("text_only", target.GetProperty("contentMode").GetString());
            Assert.Equal(0, target.GetProperty("media").GetArrayLength());
            Assert.Equal(TextGolden, json.RootElement.GetProperty("contentHash").GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        });
        var client = new PostizClient(new HttpClient(handler) { BaseAddress = new("https://postiz.invalid/") },
            new PostizOptions { BaseAddress = new("https://postiz.invalid/"), ApiKey = "synthetic-unit-only", OrganizationId = "pharmacy-1", MaxRetryAttempts = 0 });
        var request = AttachRenderedRequest.Create("occ-1", "synthetic-render-token", Correlation,
            [new("integration-1", "facebook", "Caffè pronto <ok>", [], RenderContentModes.TextOnly)], At);
        await client.PrePublishRender.AttachRenderedAsync("occ-1", request, "attach-text-v3");
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Media_and_unicode_text_match_the_engine_canonical_goldens()
    {
        Assert.Equal("prepublish-render/v3", RenderContentModes.ContractVersion);
        Assert.Equal(TextGolden, PrePublishRenderHash.Compute("occ-1", Correlation,
            [new("integration-1", "facebook", "Caffè pronto <ok>", [], RenderContentModes.TextOnly)], At));
        Assert.Equal(MediaGolden, PrePublishRenderHash.Compute("occ-1", Correlation,
            [new("integration-1", "youtube", "Ready caption", [new("media-1", "video", "video/mp4")], RenderContentModes.Media)], At));
        Assert.NotEqual(TextGolden, PrePublishRenderHash.Compute("occ-1", Correlation,
            [new("integration-1", "facebook", "Changed caption", [], RenderContentModes.TextOnly)], At));
    }

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("legacy")]
    public void Omitted_or_unknown_content_mode_never_defaults_to_media(string? mode)
        => Assert.Throws<ArgumentException>(() => PrePublishRenderHash.Compute("occ-1", Correlation,
            [new("integration-1", "facebook", "caption", [], mode!)], At));

    [Theory]
    [InlineData("caption")][InlineData("media")][InlineData("thumbnail")][InlineData("story")]
    public void Text_mode_requires_its_exact_shape(string changed)
    {
        var target = new RenderTarget("integration-1", "facebook", changed == "caption" ? "  " : "caption",
            changed == "media" ? [new("m", "image", "image/png")] : [], RenderContentModes.TextOnly,
            changed == "thumbnail" ? new(ThumbnailMediaId: "m") : null,
            changed == "story" ? RenderPublishModes.StorySequence : null);
        Assert.Throws<ArgumentException>(() => PrePublishRenderHash.Compute("occ-1", Correlation, [target], At));
    }

    [Theory]
    [InlineData(0)][InlineData(11)]
    public void Media_mode_preserves_one_to_ten_items(int count)
        => Assert.Throws<ArgumentException>(() => PrePublishRenderHash.Compute("occ-1", Correlation,
            [new("integration-1", "facebook", "caption", Enumerable.Range(0, count).Select(i => new RenderedMedia("m" + i, "image", "image/png")).ToArray(), RenderContentModes.Media)], At));

    [Fact]
    public void Text_mode_does_not_mean_zero_or_multiple_targets()
    {
        var target = new RenderTarget("integration-1", "facebook", "caption", [], RenderContentModes.TextOnly);
        Assert.Throws<ArgumentException>(() => PrePublishRenderHash.Compute("occ-1", Correlation, [], At));
        Assert.Throws<ArgumentException>(() => PrePublishRenderHash.Compute("occ-1", Correlation, [target, target], At));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}