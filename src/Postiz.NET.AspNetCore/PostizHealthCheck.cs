using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace Postiz.AspNetCore;

public sealed class PostizHealthCheck(IPostizClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.Health.IsConnectedAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy("Postiz public API is reachable.")
                : HealthCheckResult.Unhealthy("Postiz public API rejected the configured credentials.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Postiz public API is unavailable.", exception);
        }
    }
}

public static class PostizHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddPostiz(
        this IHealthChecksBuilder builder,
        string name = "postiz",
        IEnumerable<string>? tags = null) =>
        builder.AddCheck<PostizHealthCheck>(name, tags: tags);
}
