using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Postiz.Transport;

namespace Postiz.PrePublishRender;

public static class PrePublishRenderStatuses
{
    public const string Scheduled = "Scheduled";
    public const string AwaitingRender = "AwaitingRender";
    public const string ReadyToPublish = "ReadyToPublish";
    public const string Publishing = "Publishing";
    public const string Published = "Published";
    public const string Failed = "Failed";
    public const string RenderTimedOut = "RenderTimedOut";
    public const string Cancelled = "Cancelled";
}

public sealed record RenderCorrelation(
    string CrmSocialPostId,
    string SnapshotId,
    string PharmacyGroupId,
    string? PharmacyId = null);

public sealed record RenderedMedia(string MediaId, string Kind, string Mime);

public sealed record RenderTargetExtras(string? YoutubeTitle = null, string? ThumbnailMediaId = null);

public sealed record RenderTarget(
    string IntegrationId,
    string Channel,
    string Caption,
    IReadOnlyList<RenderedMedia> Media,
    RenderTargetExtras? Extras = null,
    string? PublishMode = null);

public static class RenderPublishModes
{
    public const string StorySequence = "story_sequence";
}

public sealed record StorySequenceChildReceipt(
    int SlideIndex,
    string MediaId,
    string ProviderId,
    string ReleaseUrl,
    string? ProviderContainerId = null,
    bool Recovered = false);

public sealed record StorySequencePublishReceipt(
    string BundleId,
    string Mode,
    string Provider,
    string Status,
    IReadOnlyList<StorySequenceChildReceipt> Children);

public sealed record ClaimRenderRequest(string WorkerId, int LeaseSeconds = 300);

public sealed record AttachRenderedRequest(
    string RenderToken,
    RenderCorrelation Correlation,
    IReadOnlyList<RenderTarget> Targets,
    DateTimeOffset RenderedAtUtc,
    string ContentHash)
{
    public static AttachRenderedRequest Create(
        string occurrenceId,
        string renderToken,
        RenderCorrelation correlation,
        IReadOnlyList<RenderTarget> targets,
        DateTimeOffset renderedAtUtc)
    {
        var hash = PrePublishRenderHash.Compute(occurrenceId, correlation, targets, renderedAtUtc);
        return new(renderToken, correlation, targets, renderedAtUtc, hash);
    }
}

public sealed record RenderOccurrence(
    string OccurrenceId,
    string SocialPostId,
    string IntegrationId,
    int Sequence,
    DateTimeOffset ScheduledFor,
    string Status,
    RenderCorrelation Correlation,
    DateTimeOffset? LeaseExpiresAt = null,
    DateTimeOffset? RenderedAtUtc = null,
    DateTimeOffset? PublishedAtUtc = null,
    string? ReleaseId = null,
    string? ReleaseUrl = null,
    string? ReasonCode = null,
    StorySequencePublishReceipt? PublishReceipt = null);

public sealed record ClaimedRenderOccurrence(
    string OccurrenceId,
    string SocialPostId,
    string IntegrationId,
    int Sequence,
    DateTimeOffset ScheduledFor,
    string Status,
    RenderCorrelation Correlation,
    DateTimeOffset? LeaseExpiresAt,
    string RenderToken);

public interface IPostizPrePublishRenderClient
{
    Task<IReadOnlyList<RenderOccurrence>> ListAsync(
        string? postId = null,
        string? status = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<RenderOccurrence> GetAsync(string occurrenceId, CancellationToken cancellationToken = default);

    Task<ClaimedRenderOccurrence> ClaimAsync(
        string occurrenceId,
        ClaimRenderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<RenderOccurrence> AttachRenderedAsync(
        string occurrenceId,
        AttachRenderedRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<RenderOccurrence> CancelAsync(string occurrenceId, CancellationToken cancellationToken = default);
}

internal sealed class PostizPrePublishRenderClient(PostizTransport transport) : IPostizPrePublishRenderClient
{
    private const string BasePath = "public/v1/prepublish-render/occurrences";

    public async Task<IReadOnlyList<RenderOccurrence>> ListAsync(
        string? postId = null,
        string? status = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"take={Math.Clamp(take, 1, 200)}" };
        if (!string.IsNullOrWhiteSpace(postId)) query.Add($"postId={Uri.EscapeDataString(postId)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        return await transport.GetAsync<RenderOccurrence[]>($"{BasePath}?{string.Join("&", query)}", cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<RenderOccurrence> GetAsync(string occurrenceId, CancellationToken cancellationToken = default) =>
        transport.GetAsync<RenderOccurrence>($"{BasePath}/{Escape(occurrenceId)}", cancellationToken);

    public Task<ClaimedRenderOccurrence> ClaimAsync(
        string occurrenceId,
        ClaimRenderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        transport.PostIdempotentAsync<ClaimedRenderOccurrence>(
            $"{BasePath}/{Escape(occurrenceId)}/claim-render", request, Required(idempotencyKey), cancellationToken);

    public Task<RenderOccurrence> AttachRenderedAsync(
        string occurrenceId,
        AttachRenderedRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        transport.PostIdempotentAsync<RenderOccurrence>(
            $"{BasePath}/{Escape(occurrenceId)}/attach-rendered", request, Required(idempotencyKey), cancellationToken);

    public Task<RenderOccurrence> CancelAsync(string occurrenceId, CancellationToken cancellationToken = default) =>
        transport.PostAsync<RenderOccurrence>($"{BasePath}/{Escape(occurrenceId)}/cancel", new { }, cancellationToken);

    private static string Escape(string value) => Uri.EscapeDataString(Required(value));

    private static string Required(string value) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A non-empty value is required.");
}

public static class PrePublishRenderHash
{
    public static string Compute(
        string occurrenceId,
        RenderCorrelation correlation,
        IReadOnlyList<RenderTarget> targets,
        DateTimeOffset renderedAtUtc)
    {
        var element = JsonSerializer.SerializeToElement(
            new { occurrenceId, correlation, targets, renderedAtUtc },
            PostizJson.Options);
        var canonical = new StringBuilder();
        AppendCanonical(element, canonical);
        return $"sha256-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant()}";
    }

    private static void AppendCanonical(JsonElement element, StringBuilder output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');
                var first = true;
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!first) output.Append(',');
                    first = false;
                    output.Append(JsonSerializer.Serialize(property.Name));
                    output.Append(':');
                    AppendCanonical(property.Value, output);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                for (var index = 0; index < element.GetArrayLength(); index++)
                {
                    if (index > 0) output.Append(',');
                    AppendCanonical(element[index], output);
                }
                output.Append(']');
                break;
            default:
                output.Append(element.GetRawText());
                break;
        }
    }
}
