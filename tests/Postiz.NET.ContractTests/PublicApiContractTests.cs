using System.Net;
using System.Text;
using Postiz;
using Xunit;

namespace Postiz.NET.ContractTests;

public sealed class PublicApiContractTests
{
    [Fact]
    public async Task Supported_v2230_integration_contract_deserializes()
    {
        using var handler = new FixtureHandler();
        var client = new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions { BaseAddress = new Uri("https://postiz.test/"), ApiKey = "key", MaxRetryAttempts = 0 });

        var integrations = await client.Integrations.GetAsync(cancellationToken: CancellationToken.None);

        var integration = Assert.Single(integrations);
        Assert.Equal("integration-1", integration.Id);
        Assert.Equal("instagram-standalone", integration.Identifier);
        Assert.Equal("pharmacy-a", integration.Customer?.Id);
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("public/v1/integrations", request.RequestUri?.PathAndQuery.TrimStart('/'));
            const string fixture = "[{\"id\":\"integration-1\",\"name\":\"Happy Pharmacy\",\"identifier\":\"instagram-standalone\",\"picture\":null,\"disabled\":false,\"profile\":\"happy\",\"customer\":{\"id\":\"pharmacy-a\",\"name\":\"Pharmacy A\"}}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fixture, Encoding.UTF8, "application/json"),
            });
        }
    }
}
