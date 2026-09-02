using System.Net;
using System.Text;
using Postiz;
using Postiz.Appliance;
using Postiz.Chat;
using Postiz.Integrations;
using Postiz.Posts;
using Postiz.PrePublishRender;
using Xunit;

namespace Postiz.NET.UnitTests;

public sealed class PostizClientTests
{
    [Fact]
    public async Task Integration_settings_expose_the_typed_public_comment_contract()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("/public/v1/integration-settings/integration-1", request.RequestUri?.AbsolutePath);
            return Json(HttpStatusCode.OK,
                """{"output":{"settings":{},"postComments":{"contractVersion":"post-comments/v1","supported":true,"firstComment":{"key":"firstComment","type":"string","description":"First public comment"},"comments":{"key":"comments","type":"array","description":"Additional comments"},"nativeRepresentation":{"path":"posts[].value[1..]","delayKey":"delay","delayUnit":"minutes"}}}}""");
        });
        var client = CreateClient(handler);

        var settings = await client.Integrations.GetSettingsAsync("integration-1");

        Assert.NotNull(settings.PostComments);
        Assert.True(settings.PostComments.Supported);
        Assert.Equal(PostizPostSettingKeys.FirstComment, settings.PostComments.FirstComment.Key);
        Assert.Equal(PostizPostSettingKeys.Comments, settings.PostComments.Comments.Key);
        Assert.Equal("minutes", settings.PostComments.NativeRepresentation.DelayUnit);
    }

    [Fact]
    public async Task Render_claim_is_tenant_scoped_typed_and_idempotent()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/public/v1/prepublish-render/occurrences/occ-1/claim-render", request.RequestUri?.AbsolutePath);
            Assert.Equal("pharmacy-42", request.Headers.GetValues("X-HappyM-Organization-Id").Single());
            Assert.Equal("claim-1", request.Headers.GetValues("Idempotency-Key").Single());
            return Json(HttpStatusCode.OK,
                "{\"occurrenceId\":\"occ-1\",\"socialPostId\":\"post-1\",\"integrationId\":\"integration-1\",\"sequence\":0,\"scheduledFor\":\"2026-08-26T09:00:00Z\",\"status\":\"AwaitingRender\",\"correlation\":{\"crmSocialPostId\":\"crm-1\",\"snapshotId\":\"snapshot-1\",\"pharmacyGroupId\":\"group-1\"},\"leaseExpiresAt\":\"2026-08-26T08:05:00Z\",\"renderToken\":\"opaque-token\"}");
        });
        var client = CreateClient(handler, "pharmacy-42");

        var result = await client.PrePublishRender.ClaimAsync(
            "occ-1", new ClaimRenderRequest("crm-worker", 300), "claim-1");

        Assert.Equal("opaque-token", result.RenderToken);
        Assert.Equal(PrePublishRenderStatuses.AwaitingRender, result.Status);
    }

    [Fact]
    public async Task Render_attach_sends_computed_hash_and_idempotency_key()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("attach-1", request.Headers.GetValues("Idempotency-Key").Single());
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("sha256-", body, StringComparison.Ordinal);
            Assert.DoesNotContain("hmproj", body, StringComparison.OrdinalIgnoreCase);
            return Json(HttpStatusCode.OK,
                "{\"occurrenceId\":\"occ-1\",\"socialPostId\":\"post-1\",\"integrationId\":\"integration-1\",\"sequence\":0,\"scheduledFor\":\"2026-08-26T09:00:00Z\",\"status\":\"ReadyToPublish\",\"correlation\":{\"crmSocialPostId\":\"crm-1\",\"snapshotId\":\"snapshot-1\",\"pharmacyGroupId\":\"group-1\"}}");
        });
        var client = CreateClient(handler, "pharmacy-42");
        var correlation = new RenderCorrelation("crm-1", "snapshot-1", "group-1");
        RenderTarget[] targets =
        [
            new("integration-1", "youtube", "Ready caption", [new("media-1", "video", "video/mp4")]),
        ];
        var request = AttachRenderedRequest.Create(
            "occ-1", "opaque-token", correlation, targets, DateTimeOffset.Parse("2026-08-26T08:00:00Z"));

        var result = await client.PrePublishRender.AttachRenderedAsync("occ-1", request, "attach-1");

        Assert.Equal(PrePublishRenderStatuses.ReadyToPublish, result.Status);
        Assert.Equal(
            "sha256-0f9e710275552e8f30ff3e9930d8ef78d6bbde337a6ae5b5c1c819f80468ed26",
            request.ContentHash);
    }

    [Fact]
    public async Task Story_sequence_attach_is_typed_and_receipt_children_are_deserialized()
    {
        using var handler = new StubHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Contains("\"publishMode\":\"story_sequence\"", body, StringComparison.Ordinal);
                Assert.True(body.IndexOf("media-1", StringComparison.Ordinal) < body.IndexOf("media-2", StringComparison.Ordinal));
                return Json(HttpStatusCode.OK,
                    "{\"occurrenceId\":\"occ-1\",\"socialPostId\":\"post-1\",\"integrationId\":\"integration-1\",\"sequence\":0,\"scheduledFor\":\"2026-09-02T09:00:00Z\",\"status\":\"ReadyToPublish\",\"correlation\":{\"crmSocialPostId\":\"crm-1\",\"snapshotId\":\"snapshot-1\",\"pharmacyGroupId\":\"group-1\"}}" );
            }

            return Json(HttpStatusCode.OK,
                "{\"occurrenceId\":\"occ-1\",\"socialPostId\":\"post-1\",\"integrationId\":\"integration-1\",\"sequence\":0,\"scheduledFor\":\"2026-09-02T09:00:00Z\",\"status\":\"Published\",\"correlation\":{\"crmSocialPostId\":\"crm-1\",\"snapshotId\":\"snapshot-1\",\"pharmacyGroupId\":\"group-1\"},\"publishReceipt\":{\"bundleId\":\"story-sequence:occ-1\",\"mode\":\"story_sequence\",\"provider\":\"instagram\",\"status\":\"Published\",\"children\":[{\"slideIndex\":0,\"mediaId\":\"media-1\",\"providerId\":\"ig-1\",\"releaseUrl\":\"https://ig.test/1\",\"recovered\":false},{\"slideIndex\":1,\"mediaId\":\"media-2\",\"providerId\":\"ig-2\",\"releaseUrl\":\"https://ig.test/2\",\"recovered\":false}]}}" );
        });
        var client = CreateClient(handler, "pharmacy-42");
        var correlation = new RenderCorrelation("crm-1", "snapshot-1", "group-1");
        RenderTarget[] targets =
        [
            new(
                "integration-1",
                "instagram",
                "Story sequence",
                [
                    new("media-1", "image", "image/png"),
                    new("media-2", "image", "image/png"),
                ],
                PublishMode: RenderPublishModes.StorySequence),
        ];
        var attach = AttachRenderedRequest.Create(
            "occ-1", "opaque-token", correlation, targets, DateTimeOffset.Parse("2026-09-02T08:00:00Z"));

        await client.PrePublishRender.AttachRenderedAsync("occ-1", attach, "attach-story");
        var published = await client.PrePublishRender.GetAsync("occ-1");

        Assert.Equal("story-sequence:occ-1", published.PublishReceipt?.BundleId);
        Assert.Equal(["media-1", "media-2"], published.PublishReceipt?.Children.Select(item => item.MediaId));
    }

    [Fact]
    public void Render_reason_codes_are_strongly_mapped()
    {
        var exception = new PostizApiException(
            HttpStatusCode.Conflict, "publish_blocked_no_render", null, "{}");

        Assert.Equal(PostizApiReasonCode.PublishBlockedNoRender, exception.ReasonCode);
    }

    [Theory]
    [InlineData("story_sequence_invalid", PostizApiReasonCode.StorySequenceInvalid)]
    [InlineData("story_sequence_unsupported", PostizApiReasonCode.StorySequenceUnsupported)]
    public void Story_sequence_reason_codes_are_strongly_mapped(
        string code,
        PostizApiReasonCode expected)
    {
        var exception = new PostizApiException(
            HttpStatusCode.UnprocessableEntity, code, null, "{}");

        Assert.Equal(expected, exception.ReasonCode);
    }

    [Fact]
    public async Task Youtube_publish_is_typed_tenant_scoped_and_uses_native_route()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/public/v1/posts/youtube/publish", request.RequestUri?.AbsolutePath);
            Assert.Equal("pharmacy-42", request.Headers.GetValues("X-HappyM-Organization-Id").Single());
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("account-1", body, StringComparison.Ordinal);
            Assert.Contains("media-1", body, StringComparison.Ordinal);
            Assert.Contains("yt-shorts", body, StringComparison.Ordinal);
            return Json(HttpStatusCode.OK,
                "{\"videoId\":\"yt-123\",\"url\":\"https://www.youtube.com/shorts/yt-123\",\"formatHint\":\"yt-shorts\",\"thumbnailApplied\":true}");
        });
        var client = CreateClient(handler, "pharmacy-42");

        var result = await client.Posts.PublishYoutubeAsync(
            new PublishYoutubeRequest(
                "account-1", "A title", YoutubeFormatHints.Shorts,
                VideoMediaId: "media-1", ThumbnailMediaId: "thumb-1"),
            CancellationToken.None);

        Assert.Equal("yt-123", result.VideoId);
        Assert.True(result.ThumbnailApplied);
    }

    [Theory]
    [InlineData("media_video_required", PostizApiReasonCode.MediaVideoRequired)]
    [InlineData("media_transcode_failed", PostizApiReasonCode.MediaTranscodeFailed)]
    [InlineData("media_format_unsupported", PostizApiReasonCode.MediaFormatUnsupported)]
    [InlineData("youtube_scope_insufficient", PostizApiReasonCode.YoutubeScopeInsufficient)]
    [InlineData("thumbnail_rejected", PostizApiReasonCode.ThumbnailRejected)]
    [InlineData("thumbnail_scope_missing", PostizApiReasonCode.ThumbnailScopeMissing)]
    public async Task Youtube_errors_expose_semantic_reason_codes(
        string code,
        PostizApiReasonCode expected)
    {
        using var handler = new StubHandler((_, _) =>
            Json(HttpStatusCode.BadRequest, $"{{\"code\":\"{code}\",\"message\":\"safe\"}}"));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<PostizApiException>(() =>
            client.Posts.PublishYoutubeAsync(
                new PublishYoutubeRequest(
                    "account-1", "A title", YoutubeFormatHints.Video,
                    VideoMediaId: "media-1"),
                CancellationToken.None));

        Assert.Equal(expected, error.ReasonCode);
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

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
    public async Task Media_list_and_delete_use_the_native_public_api()
    {
        var calls = 0;
        using var handler = new StubHandler((request, _) =>
        {
            calls++;
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/public/v1/media?page=2&search=summer%20sale", request.RequestUri?.PathAndQuery);
                return Json(HttpStatusCode.OK,
                    "{\"pages\":3,\"results\":[{\"id\":\"media-1\",\"name\":\"photo.png\",\"path\":\"/photo.png\",\"createdAt\":\"2026-08-13T00:00:00Z\"}]}");
            }

            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/public/v1/media/media%2F1", request.RequestUri?.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler, "pharmacy-42");

        var page = await client.Media.ListAsync(2, " summer sale ", CancellationToken.None);
        await client.Media.DeleteAsync("media/1", CancellationToken.None);

        Assert.Equal(3, page.Pages);
        Assert.Equal("media-1", Assert.Single(page.Results).Id);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Chat_send_and_get_thread_use_the_organization_scoped_public_api()
    {
        var calls = 0;
        using var handler = new StubHandler((request, _) =>
        {
            calls++;
            Assert.Equal("pharmacy-42", request.Headers.GetValues("X-HappyM-Organization-Id").Single());
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("/public/v1/chat/messages", request.RequestUri?.AbsolutePath);
                return Json(HttpStatusCode.OK, "{\"threadId\":\"thread-1\",\"message\":\"Pronto\"}");
            }

            Assert.Equal("/public/v1/chat/threads/thread-1", request.RequestUri?.AbsolutePath);
            return Json(HttpStatusCode.OK, "{\"threadId\":\"thread-1\",\"messages\":[]}");
        });
        var client = CreateClient(handler, "pharmacy-42");

        var reply = await client.Chat.SendMessageAsync(
            new PostizChatMessageRequest("Prepara il piano", "thread-1"),
            CancellationToken.None);
        var thread = await client.Chat.GetThreadAsync(reply.ThreadId, CancellationToken.None);

        Assert.Equal("Pronto", reply.Message);
        Assert.Empty(thread.Messages);
        Assert.Equal(2, calls);
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
