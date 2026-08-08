using Postiz.Transport;

namespace Postiz.Authentication;

public interface IPostizHealthClient
{
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);
}

internal sealed class PostizHealthClient(PostizTransport transport) : IPostizHealthClient
{
    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        var response = await transport.GetAsync<ConnectedResponse>("public/v1/is-connected", cancellationToken).ConfigureAwait(false);
        return response.Connected;
    }

    private sealed record ConnectedResponse(bool Connected);
}
