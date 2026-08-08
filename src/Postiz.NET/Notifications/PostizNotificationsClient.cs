using System.Text.Json;
using Postiz.Transport;

namespace Postiz.Notifications;

public interface IPostizNotificationsClient
{
    Task<JsonElement> GetAsync(int page = 0, CancellationToken cancellationToken = default);
}

internal sealed class PostizNotificationsClient(PostizTransport transport) : IPostizNotificationsClient
{
    public Task<JsonElement> GetAsync(int page = 0, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        return transport.GetAsync<JsonElement>($"public/v1/notifications?page={page}", cancellationToken);
    }
}
