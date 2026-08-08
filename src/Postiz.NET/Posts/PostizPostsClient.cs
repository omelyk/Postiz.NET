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
    string? Group = null);

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

public sealed record CreatedPost(string PostId, string Integration);

public sealed record GetPostsRequest(DateTimeOffset StartDate, DateTimeOffset EndDate, string? CustomerId = null);

public sealed record PostizPostsPage(JsonElement[] Posts);

public interface IPostizPostsClient
{
    Task<PostizPostsPage> GetAsync(GetPostsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreatedPost>> CreateAsync(CreatePostRequest request, CancellationToken cancellationToken = default);

    Task<DateTimeOffset> FindSlotAsync(string integrationId, CancellationToken cancellationToken = default);

    Task<JsonElement> GetMissingContentAsync(string postId, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(string postId, string status, CancellationToken cancellationToken = default);

    Task DeleteAsync(string postId, CancellationToken cancellationToken = default);

    Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default);
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

    private sealed record FindSlotResponse(DateTimeOffset Date);
}
