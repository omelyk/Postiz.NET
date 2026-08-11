using System.Net;
using System.Text;
using Postiz;
using Postiz.Appliance;
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
        Assert.Equal(PostizApiReasonCode.BadRequest, error.ReasonCode);
        Assert.False(error.IsTransient);
        Assert.Equal("invalid", error.ErrorCode);
        Assert.StartsWith("Social Manager returned HTTP 400.", error.Message, StringComparison.Ordinal);
        Assert.Equal("invalid", error.Code);
        Assert.Equal("corr-123", error.CorrelationId);
        Assert.DoesNotContain("secret", error.ResponseBody, StringComparison.Ordinal);
        Assert.Contains("[redacted]", error.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gateway_error_exposes_transient_reason_without_legacy_branding()
    {
        using var handler = new StubHandler((_, _) =>
            Json(HttpStatusCode.BadGateway, "<html>gateway unavailable</html>"));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<PostizApiException>(
            () => client.Groups.GetAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
        Assert.Equal(PostizApiReasonCode.BadGateway, error.ReasonCode);
        Assert.True(error.IsTransient);
        Assert.DoesNotContain("Postiz returned", error.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task Capabilities_and_provider_catalog_are_exposed()
    {
        using var handler = new StubHandler((request, _) => request.RequestUri?.AbsolutePath switch
        {
            "/public/v1/version" => Json(HttpStatusCode.OK,
                "{\"product\":\"HappyM.Postiz\",\"apiVersion\":\"1\",\"upstreamVersion\":\"2.23.0\",\"forkVersion\":\"1.0.0-alpha.4\",\"capabilities\":[\"providers\"]}"),
            "/public/v1/providers" => Json(HttpStatusCode.OK,
                "{\"social\":[{\"name\":\"Instagram\",\"identifier\":\"instagram\",\"isExternal\":false,\"isWeb3\":false,\"isChromeExtension\":false}],\"article\":[]}"),
            _ => Json(HttpStatusCode.NotFound, "{}"),
        });
        var client = CreateClient(handler);

        var capabilities = await client.Capabilities.GetAsync(CancellationToken.None);
        var providers = await client.Integrations.GetProvidersAsync(CancellationToken.None);

        Assert.Equal("2.23.0", capabilities.UpstreamVersion);
        Assert.Equal("instagram", Assert.Single(providers.Social).Identifier);
    }

    [Fact]
    public async Task Webhook_update_requires_an_id_before_sending()
    {
        using var handler = new StubHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"webhook-1\"}"));
        var client = CreateClient(handler);
        var request = new Webhooks.PostizWebhookRequest(
            "CRM callback",
            new Uri("https://crm.example.test/hooks/postiz"),
            []);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Webhooks.UpdateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Release_id_lookup_is_url_encoded()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("/public/v1/posts/by-release-id/social%2F123", request.RequestUri?.AbsolutePath);
            return Json(HttpStatusCode.OK, "{\"id\":\"post-1\"}");
        });
        var client = CreateClient(handler);

        var post = await client.Posts.GetByReleaseIdAsync("social/123", CancellationToken.None);

        Assert.Equal("post-1", post.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Appliance_control_plane_uses_internal_credentials_without_api_key()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("/internal/happym/appliance/linked", request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer internal-secret", request.Headers.GetValues("Authorization").Single());
            Assert.Equal("pharma", request.Headers.GetValues("X-HappyM-Client-Id").Single());
            return Json(HttpStatusCode.OK,
                "{\"up\":true,\"apiOk\":true,\"applianceMode\":true,\"ready\":true,\"productName\":\"Social Manager\",\"systemOrganizationId\":\"happym-system\",\"adminProvisioned\":true,\"serviceKeyProvisioned\":true}");
        });
        var client = new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions
            {
                BaseAddress = new Uri("https://postiz.test/"),
                InternalClientId = "pharma",
                InternalClientSecret = "internal-secret",
                MaxRetryAttempts = 0,
            });

        var status = await client.Appliance.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Ready);
        Assert.Equal("Social Manager", status.ProductName);
    }

    [Fact]
    public async Task Tenant_scoped_public_api_sends_the_organization_header()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("pharmacy-42", request.Headers.GetValues("X-HappyM-Organization-Id").Single());
            return Json(HttpStatusCode.OK, "[]");
        });
        var client = CreateClient(handler, "pharmacy-42");

        await client.Integrations.GetAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public async Task Facebook_oauth_app_is_hot_applied_over_the_internal_control_plane()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/appliance/providers/facebook", request.RequestUri?.AbsolutePath);
            Assert.Equal("Bearer internal-secret", request.Headers.GetValues("Authorization").Single());
            Assert.Equal("pharma", request.Headers.GetValues("X-HappyM-Client-Id").Single());
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("facebook-app-id", body, StringComparison.Ordinal);
            Assert.Contains("facebook-app-secret", body, StringComparison.Ordinal);
            return Json(HttpStatusCode.OK,
                "{\"configured\":true,\"appIdMasked\":\"*********p-id\"}");
        });
        var client = new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions
            {
                BaseAddress = new Uri("https://postiz.test/"),
                InternalClientId = "pharma",
                InternalClientSecret = "internal-secret",
                MaxRetryAttempts = 0,
            });

        var result = await client.Appliance.SetFacebookOAuthAppAsync(
            new("facebook-app-id", "facebook-app-secret"), CancellationToken.None);

        Assert.True(result.Configured);
        Assert.Equal("*********p-id", result.AppIdMasked);
    }

    [Fact]
    public async Task Provider_status_does_not_expose_a_secret_contract()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("/appliance/providers", request.RequestUri?.AbsolutePath);
            return Json(HttpStatusCode.OK,
                "{\"providers\":[{\"provider\":\"facebook\",\"configured\":true,\"appIdMasked\":\"********1234\"}]}");
        });
        var client = new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions
            {
                BaseAddress = new Uri("https://postiz.test/"),
                InternalClientId = "pharma",
                InternalClientSecret = "internal-secret",
                MaxRetryAttempts = 0,
            });

        var status = await client.Appliance.GetProvidersStatusAsync(CancellationToken.None);

        var facebook = Assert.Single(status.Providers);
        Assert.True(facebook.Configured);
        Assert.Equal("********1234", facebook.AppIdMasked);
        Assert.DoesNotContain(
            typeof(ProviderOAuthAppStatus).GetProperties(),
            property => property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rotated_service_key_is_used_by_following_public_api_calls()
    {
        var calls = 0;
        using var handler = new StubHandler((request, _) =>
        {
            calls++;
            if (request.RequestUri?.AbsolutePath.EndsWith("credentials/rotate", StringComparison.Ordinal) == true)
            {
                Assert.Equal("Bearer internal-secret", request.Headers.GetValues("Authorization").Single());
                return Json(HttpStatusCode.OK,
                    "{\"apiKey\":\"rotated-key\",\"organizationId\":\"happym-system\",\"issuedAt\":\"2026-08-08T00:00:00Z\",\"rotated\":true}");
            }

            Assert.Equal("rotated-key", request.Headers.GetValues("Authorization").Single());
            return Json(HttpStatusCode.OK, "[]");
        });
        var client = new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions
            {
                BaseAddress = new Uri("https://postiz.test/"),
                ApiKey = "old-key",
                InternalClientId = "pharma",
                InternalClientSecret = "internal-secret",
                MaxRetryAttempts = 0,
            });

        await client.Appliance.RotateCredentialsAsync(CancellationToken.None);
        await client.Integrations.GetAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(2, calls);
    }

    private static IPostizClient CreateClient(HttpMessageHandler handler, string? organizationId = null) =>
        new PostizClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://postiz.test/") },
            new PostizOptions
            {
                BaseAddress = new Uri("https://postiz.test/"),
                ApiKey = "postiz-api-key",
                OrganizationId = organizationId,
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
