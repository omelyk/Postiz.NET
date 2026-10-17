using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Postiz.Posts;
using Postiz.Transport;

namespace Postiz.ScheduleIntents;

/// <summary>A frozen schedule envelope and the hash of its exact UTF-8 representation.</summary>
public sealed record ScheduleIntentRequest(string PayloadJson, string PayloadHash);

public sealed record ScheduleIntentLink(string IntegrationId, string PostId, string OccurrenceId);

/// <summary>A durable scheduling receipt, not a confirmation of social publication.</summary>
public sealed record ScheduleIntentReceipt(
    string Contract,
    string IntentId,
    string PayloadHash,
    string CommandId,
    string CrmOccurrenceId,
    string RevisionId,
    string BindingKey,
    string OrganizationId,
    string State,
    string DispatchState,
    IReadOnlyList<ScheduleIntentLink> Links);

public interface IPostizScheduleIntentsClient
{
    Task<ScheduleIntentReceipt> CreateAsync(
        string intentId, ScheduleIntentRequest request, CancellationToken cancellationToken = default);

    Task<ScheduleIntentReceipt> GetAsync(string intentId, CancellationToken cancellationToken = default);

    Task<ScheduleIntentReceipt> CancelAsync(
        string intentId, ScheduleIntentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Additive client for editorial.schedule-intent/v1. Legacy clients remain unchanged.</summary>
public sealed class PostizScheduleIntentsClient : IPostizScheduleIntentsClient
{
    private const string BasePath = "public/v1/prepublish-render/schedule-intents";
    private readonly PostizTransport transport;

    public PostizScheduleIntentsClient(HttpClient httpClient, PostizOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        httpClient.BaseAddress ??= options.BaseAddress;
        transport = new PostizTransport(httpClient, options);
    }

    public async Task<ScheduleIntentReceipt> CreateAsync(
        string intentId, ScheduleIntentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = IntentPath(intentId);
        ScheduleIntentPayload.ValidateFrozen(request);
        return await transport.PostIdempotentAsync<ScheduleIntentReceipt>(path, request, intentId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ScheduleIntentReceipt> GetAsync(string intentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await transport.GetAsync<ScheduleIntentReceipt>(IntentPath(intentId), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ScheduleIntentReceipt> CancelAsync(
        string intentId, ScheduleIntentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = IntentPath(intentId) + "/cancel";
        ScheduleIntentPayload.ValidateFrozen(request);
        return await transport.PostIdempotentAsync<ScheduleIntentReceipt>(path, request, intentId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string IntentPath(string intentId)
    {
        if (string.IsNullOrEmpty(intentId) || intentId.Length > 128
            || intentId.Any(c => !((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9') || c is '_' or '-')))
            throw new ArgumentException("A schedule intent ID must be an ASCII token of 1 to 128 characters.", nameof(intentId));
        return BasePath + "/" + Uri.EscapeDataString(intentId);
    }
}

/// <summary>Build once, then durably retain the returned strings for create, lookup and cancel recovery.</summary>
public static class ScheduleIntentPayload
{
    public const string Contract = "editorial.schedule-intent/v1";
    public const int MaximumPayloadBytes = 256 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static ScheduleIntentRequest Create(
        string commandId, string crmOccurrenceId, string revisionId, string bindingKey, CreatePostRequest post)
    {
        Required(commandId, nameof(commandId));
        Required(crmOccurrenceId, nameof(crmOccurrenceId));
        Required(revisionId, nameof(revisionId));
        Required(bindingKey, nameof(bindingKey));
        ValidatePost(post);
        // Keep the same post wire projection and serializer as the legacy Posts.Create path.
        var payloadJson = JsonSerializer.Serialize(new
        {
            contract = Contract,
            commandId,
            crmOccurrenceId,
            revisionId,
            bindingKey,
            post = new
            {
                type = post.Type.ToString().ToLowerInvariant(),
                date = post.Date.ToString("O"),
                posts = post.Posts,
                post.ShortLink,
                tags = post.Tags ?? [],
                post.Inter,
                post.Order,
                post.CreationMethod,
            }
        }, PostizJson.Options);
        return new ScheduleIntentRequest(payloadJson, ComputeHash(payloadJson));
    }

    /// <summary>Hashes the original string without parsing, trimming or canonicalizing JSON.</summary>
    public static string ComputeHash(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        int size;
        try { size = Utf8.GetByteCount(payloadJson); }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("The frozen schedule payload must contain valid UTF-8 text.", nameof(payloadJson));
        }
        if (size == 0 || size > MaximumPayloadBytes)
            throw new ArgumentException("The frozen schedule payload must contain 1 to 262144 UTF-8 bytes.", nameof(payloadJson));
        return "sha256-" + Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(payloadJson))).ToLowerInvariant();
    }

    internal static void ValidateFrozen(ScheduleIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(ComputeHash(request.PayloadJson), request.PayloadHash, StringComparison.Ordinal))
            throw new ArgumentException("The frozen schedule payload hash does not match its exact UTF-8 bytes.", nameof(request));
        // Treat persisted payload bytes as opaque. The server validates the envelope and scope.
        // Reconstructing or normalizing this string here would change the durable intent.
    }

    private static void ValidatePost(CreatePostRequest post)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (post.Type != PostizPostType.Schedule || post.ShortLink || post.Inter is not null
            || post.Order is not null || post.Tags is { Count: > 0 } || post.Posts is not { Count: 1 })
            throw new ArgumentException("Schedule intent v1 requires one scheduled target without tags, interval, order or short links.", nameof(post));
        var target = post.Posts[0];
        if (target is null || target.Group is not null || target.Integration is null
            || string.IsNullOrWhiteSpace(target.Integration.Id) || target.Value is not { Count: > 0 }
            || target.Value.Any(v => v is null || v.Id is not null)
            || string.IsNullOrWhiteSpace(target.Value[0].Content)
            || target.PrePublishRender?.Correlation is not { } correlation)
            throw new ArgumentException("A schedule target requires integration, root content, render correlation and no edit IDs or groups.", nameof(post));
        Required(correlation.CrmSocialPostId, nameof(post));
        Required(correlation.SnapshotId, nameof(post));
        Required(correlation.PharmacyGroupId, nameof(post));
        Required(correlation.PharmacyId, nameof(post));
    }

    private static void Required(string? value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A nonempty schedule intent field is required.", parameter);
    }
}
