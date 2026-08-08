using Postiz;
using Xunit;

namespace Postiz.NET.IntegrationTests;

public sealed class PostizIntegrationTests
{
    [Fact]
    public async Task Configured_instance_accepts_public_api_credentials()
    {
        var url = Environment.GetEnvironmentVariable("POSTIZ_TEST_URL");
        var apiKey = Environment.GetEnvironmentVariable("POSTIZ_TEST_API_KEY");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        using var httpClient = new HttpClient();
        var client = new PostizClient(httpClient, new PostizOptions
        {
            BaseAddress = new Uri(url),
            ApiKey = apiKey,
        });

        Assert.True(await client.Health.IsConnectedAsync(CancellationToken.None));
    }
}
