using Microsoft.Extensions.DependencyInjection;

namespace Postiz.DependencyInjection;

public static class PostizServiceCollectionExtensions
{
    public static IHttpClientBuilder AddPostizClient(
        this IServiceCollection services,
        Action<PostizOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new PostizOptions
        {
            BaseAddress = new Uri("https://localhost"),
            ApiKey = string.Empty,
        };
        configure(options);
        options.Validate();
        services.AddSingleton(options);

        return services.AddHttpClient<IPostizClient, PostizClient>(client =>
        {
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
    }
}
