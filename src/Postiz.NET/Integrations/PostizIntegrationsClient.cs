using System.Text.Json;
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

public sealed record PostizIntegrationSettings(JsonElement Output);

public sealed record PostizToolRequest(string MethodName, IReadOnlyDictionary<string, string> Data);

public interface IPostizGroupsClient
{
    Task<IReadOnlyList<PostizGroup>> GetAsync(CancellationToken cancellationToken = default);
}

public interface IPostizIntegrationsClient
{
    Task<IReadOnlyList<PostizIntegration>> GetAsync(string? groupId = null, CancellationToken cancellationToken = default);

    Task<PostizIntegrationSettings> GetSettingsAsync(string integrationId, CancellationToken cancellationToken = default);

    Task<JsonElement> TriggerAsync(string integrationId, PostizToolRequest request, CancellationToken cancellationToken = default);
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

    private sealed record ToolResponse(JsonElement Output);
}
