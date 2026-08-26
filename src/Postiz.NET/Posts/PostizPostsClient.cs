using System.Text.Json;
using System.Text.Json.Serialization;
using Postiz.Transport;

namespace Postiz.Posts;

[JsonConverter(typeof(JsonStringEnumConverter<PostizPostType>))]
public enum PostizPostType
{
    Draft,
    Schedule,
    Now,
    Update,
}

public sealed record PostizMediaReference(string? Id = null, string? Path = null);

public sealed record PostizPostContent(
    string Content,
    IReadOnlyList<PostizMediaReference> Image,
    string? Id = null,
    int? Delay = null);

public sealed record PostizIntegrationReference(string Id);

public sealed record PostizPostTarget(
    PostizIntegrationReference Integration,
    IReadOnlyList<PostizPostContent> Value,
    JsonElement Settings,
    string? Group = null,
    PrePublishRenderConfig? PrePublishRender = null);

public sealed record PrePublishRenderCorrelation(
    string CrmSocialPostId,
    string SnapshotId,
    string PharmacyGroupId,
    string? PharmacyId = null);

public sealed record PrePublishRenderConfig(
    PrePublishRenderCorrelation Correlation,
    int LeadTimeSeconds = 600);

public sealed record PostizTag(string Value, string Label);

public sealed record CreatePostRequest(
    PostizPostType Type,
    DateTimeOffset Date,
    IReadOnlyList<PostizPostTarget> Posts,
    bool ShortLink = false,
    IReadOnlyList<PostizTag>? Tags = null,
    int? Inter = null,
    string? Order = null,
    string CreationMethod = "API");

public sealed record CreatedPost(string PostId, string Integration, string? OccurrenceId = null);

public sealed record GetPostsRequest(DateTimeOffset StartDate, DateTimeOffset EndDate, string? CustomerId = null);

public sealed record PostizPostsPage(JsonElement[] Posts);

public static class YoutubeFormatHints
{
    public const string Video = "yt-video";
    public const string Shorts = "yt-shorts";
}

public sealed record PublishYoutubeRequest(
    string AccountId,
    string Title,
    string FormatHint,
    string? VideoMediaId = null,
    string? VideoPath = null,
    string? Description = null,
    string? ThumbnailMediaId = null,
    string? ThumbnailPath = null);

public sealed record PublishedYoutubeVideo(
    string VideoId,
    string Url,
    string FormatHint,
    bool ThumbnailApplied);

public interface IPostizPostsClient
{
    Task<PostizPostsPage> GetAsync(GetPostsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatedPost>> CreateAsync(CreatePostRequest request, CancellationToken cancellationToken = default);

    Task<PublishedYoutubeVideo> PublishYoutubeAsync(
        PublishYoutubeRequest request,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset> FindSlotAsync(string integrationId, CancellationToken cancellationToken = default);

    Task<JsonElement> GetMissingContentAsync(string postId, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(string postId, string status, CancellationToken cancellationToken = default);

    Task DeleteAsync(string postId, CancellationToken cancellationToken = default);

    Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default);

    Task UpdateReleaseIdAsync(string postId, string releaseId, CancellationToken cancellationToken = default);

    Task<JsonElement> GetByReleaseIdAsync(string releaseId, CancellationToken cancellationToken = default);
}

internal sealed class PostizPostsClient(PostizTransport transport) : IPostizPostsClient
{
    public Task<PostizPostsPage> GetAsync(GetPostsRequest request, CancellationToken cancellationToken = default)
    {
        var query = $"startDate={Uri.EscapeDataString(request.StartDate.ToString("O"))}&endDate={Uri.EscapeDataString(request.EndDate.ToString("O"))}";
        if (!string.IsNullOrWhiteSpace(request.CustomerId))
        {
            query += $"&customer={Uri.EscapeDataString(request.CustomerId)}";
        }

        return transport.GetAsync<PostizPostsPage>($"public/v1/posts?{query}", cancellationToken);
    }

    public async Task<IReadOnlyList<CreatedPost>> CreateAsync(
        CreatePostRequest request,
        CancellationToken cancellationToken = default) =>
        await transport.PostAsync<CreatedPost[]>(
            "public/v1/posts",
            new
            {
                type = request.Type.ToString().ToLowerInvariant(),
                date = request.Date.ToString("O"),
                posts = request.Posts,
                request.ShortLink,
                tags = request.Tags ?? [],
                request.Inter,
                request.Order,
                request.CreationMethod,
            },
            cancellationToken).ConfigureAwait(false);

    public Task<PublishedYoutubeVideo> PublishYoutubeAsync(
        PublishYoutubeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.VideoMediaId) && string.IsNullOrWhiteSpace(request.VideoPath))
        {
            throw new ArgumentException("A video media ID or tenant-owned path is required.", nameof(request));
        }
        if (request.FormatHint is not (YoutubeFormatHints.Video or YoutubeFormatHints.Shorts))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "FormatHint must be 'yt-video' or 'yt-shorts'.");
        }

        return transport.PostYoutubeAsync<PublishedYoutubeVideo>(
            "public/v1/posts/youtube/publish",
            request,
            cancellationToken);
    }

    public async Task<DateTimeOffset> FindSlotAsync(
        string integrationId,
        CancellationToken cancellationToken = default)
    {
        var response = await transport.GetAsync<FindSlotResponse>(
            $"public/v1/find-slot/{Uri.EscapeDataString(integrationId)}",
            cancellationToken).ConfigureAwait(false);
        return response.Date;
    }

    public Task<JsonElement> GetMissingContentAsync(string postId, CancellationToken cancellationToken = default) =>
        transport.GetAsync<JsonElement>($"public/v1/posts/{Uri.EscapeDataString(postId)}/missing", cancellationToken);

    public async Task ChangeStatusAsync(
        string postId,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (status is not ("draft" or "schedule"))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Status must be 'draft' or 'schedule'.");
        }

        _ = await transport.PutAsync<JsonElement>(
            $"public/v1/posts/{Uri.EscapeDataString(postId)}/status",
            new { status },
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string postId, CancellationToken cancellationToken = default) =>
        transport.DeleteAsync($"public/v1/posts/{Uri.EscapeDataString(postId)}", cancellationToken);

    public Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default) =>
        transport.DeleteAsync($"public/v1/posts/group/{Uri.EscapeDataString(groupId)}", cancellationToken);

    public async Task UpdateReleaseIdAsync(
        string postId,
        string releaseId,
        CancellationToken cancellationToken = default)
    {
        _ = await transport.PutAsync<JsonElement>(
            $"public/v1/posts/{Uri.EscapeDataString(postId)}/release-id",
            new { releaseId },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<JsonElement> GetByReleaseIdAsync(
        string releaseId,
        CancellationToken cancellationToken = default) =>
        transport.GetAsync<JsonElement>(
            $"public/v1/posts/by-release-id/{Uri.EscapeDataString(releaseId)}",
            cancellationToken);

    private sealed record FindSlotResponse(DateTimeOffset Date);
}
