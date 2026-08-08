using Postiz.Transport;

namespace Postiz.Capabilities;

public sealed record PostizCapabilities(
    string Product,
    string ApiVersion,
    string UpstreamVersion,
    string ForkVersion,
    IReadOnlyList<string> Capabilities);

public interface IPostizCapabilitiesClient
{
    Task<PostizCapabilities> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed class PostizCapabilitiesClient(PostizTransport transport) : IPostizCapabilitiesClient
{
    public Task<PostizCapabilities> GetAsync(CancellationToken cancellationToken = default) =>
        transport.GetAsync<PostizCapabilities>("public/v1/version", cancellationToken);
}
