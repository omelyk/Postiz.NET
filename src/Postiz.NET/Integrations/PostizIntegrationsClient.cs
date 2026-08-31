using System.Text.Json;
using System.Text.Json.Serialization;
using Postiz.Transport;

namespace Postiz.Integrations;

public sealed record PostizGroup(string Id, string Name);

public sealed record PostizIntegrationCustomer(string Id, string Name);

public sealed record PostizIntegration(
    string Id,
    string Name,
    string Identifier,
    string? Picture,
    bool Disabled,
    string? Profile,
    PostizIntegrationCustomer? Customer);

public static class PostizPostSettingKeys
{
    public const string FirstComment = "firstComment";
    public const string Comments = "comments";
    public const string ValidUntil = "validUntil";
}

public sealed record PostizSettingKey(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record PostizNativeCommentRepresentation(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("delayKey")] string DelayKey,
    [property: JsonPropertyName("delayUnit")] string DelayUnit);

public sealed record PostizPostCommentsContract(
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("supported")] bool Supported,
    [property: JsonPropertyName("firstComment")] PostizSettingKey FirstComment,
    [property: JsonPropertyName("comments")] PostizSettingKey Comments,
    [property: JsonPropertyName("nativeRepresentation")] PostizNativeCommentRepresentation NativeRepresentation);

public sealed record PostizIntegrationSettings(JsonElement Output)
{
    public PostizPostCommentsContract? PostComments =>
        Output.ValueKind == JsonValueKind.Object
        && Output.TryGetProperty("postComments", out var contract)
            ? contract.Deserialize<PostizPostCommentsContract>()
            : null;
}

public sealed record PostizToolRequest(string MethodName, IReadOnlyDictionary<string, string> Data);

public sealed record PostizProvider(
    string Name,
    string Identifier,
    string? ToolTip,
    string? Editor,
    bool IsExternal,
    bool IsWeb3,
    bool IsChromeExtension,
    JsonElement? CustomFields);

public sealed record PostizProviderCatalog(IReadOnlyList<PostizProvider> Social, JsonElement Article);

public sealed record PostizConnectUrl(string Url);

public interface IPostizGroupsClient
{
    Task<IReadOnlyList<PostizGroup>> GetAsync(CancellationToken cancellationToken = default);
}

public interface IPostizIntegrationsClient
{
    Task<IReadOnlyList<PostizIntegration>> GetAsync(string? groupId = null, CancellationToken cancellationToken = default);

    Task<PostizIntegrationSettings> GetSettingsAsync(string integrationId, CancellationToken cancellationToken = default);

    Task<JsonElement> TriggerAsync(string integrationId, PostizToolRequest request, CancellationToken cancellationToken = default);

    Task<PostizProviderCatalog> GetProvidersAsync(CancellationToken cancellationToken = default);

    Task<PostizConnectUrl> GetConnectUrlAsync(
        string providerIdentifier,
        string? refreshIntegrationId = null,
        CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(
        string integrationId,
        JsonElement additionalSettings,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string integrationId, CancellationToken cancellationToken = default);
}

internal sealed class PostizGroupsClient(PostizTransport transport) : IPostizGroupsClient
{
    public async Task<IReadOnlyList<PostizGroup>> GetAsync(CancellationToken cancellationToken = default) =>
        await transport.GetAsync<PostizGroup[]>("public/v1/groups", cancellationToken).ConfigureAwait(false);
}

internal sealed class PostizIntegrationsClient(PostizTransport transport) : IPostizIntegrationsClient
{
    public async Task<IReadOnlyList<PostizIntegration>> GetAsync(
        string? groupId = null,
        CancellationToken cancellationToken = default)
    {
        var path = "public/v1/integrations";
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            path += $"?group={Uri.EscapeDataString(groupId)}";
        }

        return await transport.GetAsync<PostizIntegration[]>(path, cancellationToken).ConfigureAwait(false);
    }

    public Task<PostizIntegrationSettings> GetSettingsAsync(
        string integrationId,
        CancellationToken cancellationToken = default) =>
        transport.GetAsync<PostizIntegrationSettings>(
            $"public/v1/integration-settings/{Uri.EscapeDataString(integrationId)}",
            cancellationToken);

    public async Task<JsonElement> TriggerAsync(
        string integrationId,
        PostizToolRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await transport.PostAsync<ToolResponse>(
            $"public/v1/integration-trigger/{Uri.EscapeDataString(integrationId)}",
            request,
            cancellationToken).ConfigureAwait(false);
        return response.Output;
    }

    public Task<PostizProviderCatalog> GetProvidersAsync(CancellationToken cancellationToken = default) =>
        transport.GetAsync<PostizProviderCatalog>("public/v1/providers", cancellationToken);

    public Task<PostizConnectUrl> GetConnectUrlAsync(
        string providerIdentifier,
        string? refreshIntegrationId = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"public/v1/social/{Uri.EscapeDataString(providerIdentifier)}";
        if (!string.IsNullOrWhiteSpace(refreshIntegrationId))
        {
            path += $"?refresh={Uri.EscapeDataString(refreshIntegrationId)}";
        }

        return transport.GetAsync<PostizConnectUrl>(path, cancellationToken);
    }

    public async Task UpdateSettingsAsync(
        string integrationId,
        JsonElement additionalSettings,
        CancellationToken cancellationToken = default)
    {
        _ = await transport.PutAsync<JsonElement>(
            $"public/v1/integrations/{Uri.EscapeDataString(integrationId)}/settings",
            new { additionalSettings },
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string integrationId, CancellationToken cancellationToken = default) =>
        transport.DeleteAsync($"public/v1/integrations/{Uri.EscapeDataString(integrationId)}", cancellationToken);

    private sealed record ToolResponse(JsonElement Output);
}
