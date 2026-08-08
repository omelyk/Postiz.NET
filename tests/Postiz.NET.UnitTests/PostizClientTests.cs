using System.Net;
using System.Text;
using Postiz;
using Xunit;

namespace Postiz.NET.UnitTests;

public sealed class PostizClientTests
{
    [Fact]
    public async Task Integrations_send_raw_api_key_and_propagate_cancellation()
    {
        using var handler = new StubHandler((request, cancellationToken) =>
        {
            Assert.Equal("postiz-api-key", request.Headers.GetValues("Authorization").Single());
            Assert.True(cancellationToken.CanBeCanceled);
            return Json(HttpStatusCode.OK, "[]");
        });
        var client = CreateClient(handler);

        var integrations = await client.Integrations.GetAsync(cancellationToken: CancellationToken.None);

        Assert.Empty(integrations);
    }

    [Fact]
    public async Task Error_response_is_typed_and_sensitive_values_are_redacted()
    {
        using var handler = new StubHandler((_, _) =>
        {
            var response = Json(HttpStatusCode.BadRequest, "{\"code\":\"invalid\",\"token\":\"secret\",\"message\":\"bad input\"}");
            response.Headers.Add("X-Correlation-Id", "corr-123");
            return response;
        });
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<PostizApiException>(
            () => client.Groups.GetAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("invalid", error.Code);
        Assert.Equal("corr-123", error.CorrelationId);
        Assert.DoesNotContain("secret", error.ResponseBody, StringComparison.Ordinal);
        Assert.Contains("[redacted]", error.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_creation_is_not_retried_on_server_error()
    {
        var attempts = 0;
        using var handler = new StubHandler((_, _) =>
        {
            attempts++;
            return Json(HttpStatusCode.ServiceUnavailable, "{\"msg\":\"unavailable\"}");
        });
        var client = CreateClient(handler);
        using var settings = System.Text.Json.JsonDocument.Parse("{\"__type\":\"x\"}");
        var request = new Posts.CreatePostRequest(
            Posts.PostizPostType.Draft,
            DateTimeOffset.UtcNow,
            [new(new("integration-1"), [new("hello", [])], settings.RootElement.Clone())]);

        await Assert.ThrowsAsync<PostizApiException>(
            () => client.Posts.CreateAsync(request, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Media_upload_does_not_dispose_the_callers_stream()
    {
        using var handler = new StubHandler((_, _) =>
            Json(HttpStatusCode.OK, "{\"id\":\"media-1\",\"name\":\"image.png\",\"path\":\"/uploads/image.png\"}"));
        var client = CreateClient(handler);
        using var stream = new MemoryStream([1, 2, 3]);

        var media = await client.Media.UploadAsync(
            stream,
            "image.png",
            "image/png",
            CancellationToken.None);

        Assert.Equal("media-1", media.Id);
        Assert.True(stream.CanRead);
    }

    private static IPostizClient CreateClient(HttpMessageHandler handler) =>
        new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions
            {
                BaseAddress = new Uri("https://postiz.test/"),
                ApiKey = "postiz-api-key",
                MaxRetryAttempts = 0,
            });

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(callback(request, cancellationToken));
    }
}
