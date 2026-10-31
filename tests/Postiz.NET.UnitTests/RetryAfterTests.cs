using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Postiz;
using Xunit;

namespace Postiz.NET.UnitTests;

public sealed class RetryAfterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Future_retry_after_does_not_reissue_read_before_cancellation(bool httpDate)
    {
        using var handler = new RateLimitedHandler(httpDate);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") };
        var client = CreateClient(http);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Integrations.GetAsync(cancellationToken: cancellation.Token));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Expired_http_date_allows_the_idempotent_read_to_recover()
    {
        using var handler = new RateLimitedHandler(true, expired: true);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") };
        var client = CreateClient(http);

        var integrations = await client.Integrations.GetAsync();

        Assert.Empty(integrations);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Retry_after_never_replays_a_write(bool httpDate)
    {
        using var handler = new RateLimitedHandler(httpDate);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") };
        var client = CreateClient(http);

        var error = await Assert.ThrowsAsync<PostizApiException>(
            () => client.Posts.ChangeStatusAsync("synthetic-post", "draft"));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    private static PostizClient CreateClient(HttpClient http) => new(http, new PostizOptions
    {
        BaseAddress = http.BaseAddress!,
        ApiKey = "synthetic-key",
        MaxRetryAttempts = 1,
        RequestTimeout = TimeSpan.FromSeconds(5),
    });

    private sealed class RateLimitedHandler(bool httpDate, bool expired = false) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var response = new HttpResponseMessage(Calls == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
            {
                Content = new StringContent(Calls == 1 ? "{}" : "[]", Encoding.UTF8, "application/json"),
            };
            if (Calls == 1)
            {
                response.Headers.RetryAfter = httpDate
                    ? new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddHours(expired ? -1 : 1))
                    : new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            }
            return Task.FromResult(response);
        }
    }
}
