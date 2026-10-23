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
    public const string SimulatedPublished = "SimulatedPublished";
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
    string ContentMode,
    RenderTargetExtras? Extras = null,
    string? PublishMode = null);

public static class RenderContentModes
{
    public const string ContractVersion = "prepublish-render/v3";
    public const string TextOnly = "text_only";
    public const string Media = "media";
}

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
        if (targets is null || targets.Count != 1 || targets[0] is not { } target || target.Media is null)
            throw new ArgumentException("Exactly one explicit rendered target is required.", nameof(targets));
        if (target.ContentMode == RenderContentModes.TextOnly)
        {
            if (string.IsNullOrWhiteSpace(target.Caption) || target.Media.Count != 0 ||
                target.Extras?.ThumbnailMediaId is not null || target.PublishMode is not null)
                throw new ArgumentException("The text_only target must contain caption and no media or media presentation.", nameof(targets));
        }
        else if (target.ContentMode != RenderContentModes.Media || target.Media.Count is < 1 or > 10)
            throw new ArgumentException("An explicit media target requires one to ten media items.", nameof(targets));
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
                    AppendCanonicalString(property.Name, output);
                    output.Append(':');
                    AppendCanonical(property.Value, output);
                }
                output.Append('}');
                break;
            case JsonValueKind.String:
                AppendCanonicalString(element.GetString()!, output);
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

    // JSON.stringify-compatible escaping; the wire serializer remains unchanged.
    private static void AppendCanonicalString(string value, StringBuilder output)
    {
        output.Append((char)34);
        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            switch (ch)
            {
                case (char)34: output.Append((char)92).Append((char)34); break;
                case (char)92: output.Append((char)92).Append((char)92); break;
                case (char)8: output.Append((char)92).Append('b'); break;
                case (char)12: output.Append((char)92).Append('f'); break;
                case (char)10: output.Append((char)92).Append('n'); break;
                case (char)13: output.Append((char)92).Append('r'); break;
                case (char)9: output.Append((char)92).Append('t'); break;
                default:
                    if (char.IsHighSurrogate(ch) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                        output.Append(ch).Append(value[++index]);
                    else if (ch < 0x20 || char.IsSurrogate(ch))
                        output.Append((char)92).Append('u').Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    else output.Append(ch);
                    break;
            }
        }
        output.Append((char)34);
    }
}