using System.Text.Json;
using Postiz.Transport;

namespace Postiz.Webhooks;

public sealed record PostizWebhookIntegration(string Id);

public sealed record PostizWebhookRequest(
    string Name,
    Uri Url,
    IReadOnlyList<PostizWebhookIntegration> Integrations,
    string? Id = null);

public sealed record PostizWebhookResult(string Id);

public interface IPostizWebhooksClient
{
    Task<JsonElement> GetAsync(CancellationToken cancellationToken = default);

    Task<PostizWebhookResult> CreateAsync(PostizWebhookRequest request, CancellationToken cancellationToken = default);

    Task<PostizWebhookResult> UpdateAsync(PostizWebhookRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string webhookId, CancellationToken cancellationToken = default);
}

internal sealed class PostizWebhooksClient(PostizTransport transport) : IPostizWebhooksClient
{
    public Task<JsonElement> GetAsync(CancellationToken cancellationToken = default) =>
        transport.GetAsync<JsonElement>("public/v1/webhooks", cancellationToken);

    public Task<PostizWebhookResult> CreateAsync(
        PostizWebhookRequest request,
        CancellationToken cancellationToken = default) =>
        transport.PostAsync<PostizWebhookResult>("public/v1/webhooks", ToBody(request), cancellationToken);

    public Task<PostizWebhookResult> UpdateAsync(
        PostizWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            throw new ArgumentException("Webhook id is required for update.", nameof(request));
        }

        return transport.PutAsync<PostizWebhookResult>("public/v1/webhooks", ToBody(request), cancellationToken);
    }

    public Task DeleteAsync(string webhookId, CancellationToken cancellationToken = default) =>
        transport.DeleteAsync($"public/v1/webhooks/{Uri.EscapeDataString(webhookId)}", cancellationToken);

    private static object ToBody(PostizWebhookRequest request) => new
    {
        request.Id,
        request.Name,
        url = request.Url.ToString(),
        request.Integrations,
    };
}
