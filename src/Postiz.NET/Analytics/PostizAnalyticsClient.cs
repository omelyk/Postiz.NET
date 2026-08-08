using System.Text.Json;
using Postiz.Transport;

namespace Postiz.Analytics;

public interface IPostizAnalyticsClient
{
    Task<JsonElement> GetIntegrationAsync(string integrationId, DateOnly date, CancellationToken cancellationToken = default);

    Task<JsonElement> GetPostAsync(string postId, long date, CancellationToken cancellationToken = default);
}

internal sealed class PostizAnalyticsClient(PostizTransport transport) : IPostizAnalyticsClient
{
    public Task<JsonElement> GetIntegrationAsync(
        string integrationId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        transport.GetAsync<JsonElement>(
            $"public/v1/analytics/{Uri.EscapeDataString(integrationId)}?date={date:yyyy-MM-dd}",
            cancellationToken);

    public Task<JsonElement> GetPostAsync(
        string postId,
        long date,
        CancellationToken cancellationToken = default) =>
        transport.GetAsync<JsonElement>(
            $"public/v1/analytics/post/{Uri.EscapeDataString(postId)}?date={date}",
            cancellationToken);
}
