using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Postiz;
using Postiz.Posts;
using Postiz.ScheduleIntents;
using Xunit;

namespace Postiz.NET.UnitTests;

// Transport fixtures only: no server, socket, provider, SQL or filesystem receipt.
public sealed class ScheduleIntentTests
{
    private static readonly DateTimeOffset Date = new(2026, 10, 20, 12, 30, 0, TimeSpan.FromHours(2));

    [Fact]
    public async Task Helper_freezes_the_same_post_wire_as_legacy_create_and_hashes_exact_utf8()
    {
        string? legacyWire = null;
        using var handler = new Handler(async (request, ct) =>
        {
            legacyWire = await request.Content!.ReadAsStringAsync(ct);
            return Json(HttpStatusCode.OK, "[]");
        });
        using var http = Http(handler);
        var post = Post();
        var frozen = ScheduleIntentPayload.Create("command-1", "crm-occ-1", "revision-1", "binding-a-test", post);
        await new PostizClient(http, Options()).Posts.CreateAsync(post);
        using var envelope = JsonDocument.Parse(frozen.PayloadJson);
        using var legacy = JsonDocument.Parse(legacyWire!);
        Assert.Equal(legacy.RootElement.GetRawText(), envelope.RootElement.GetProperty("post").GetRawText());
        Assert.Equal(ScheduleIntentPayload.Contract, envelope.RootElement.GetProperty("contract").GetString());
        Assert.Equal("command-1", envelope.RootElement.GetProperty("commandId").GetString());
        Assert.Equal("crm-occ-1", envelope.RootElement.GetProperty("crmOccurrenceId").GetString());
        Assert.Equal("revision-1", envelope.RootElement.GetProperty("revisionId").GetString());
        Assert.Equal("binding-a-test", envelope.RootElement.GetProperty("bindingKey").GetString());
        var wire = envelope.RootElement.GetProperty("post");
        Assert.Equal("schedule", wire.GetProperty("type").GetString());
        Assert.Equal(Date.ToString("O"), wire.GetProperty("date").GetString());
        Assert.Equal(2, wire.GetProperty("posts")[0].GetProperty("value").GetArrayLength());
        Assert.Equal("sha256-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(frozen.PayloadJson)))
            .ToLowerInvariant(), frozen.PayloadHash);
        Assert.NotEqual(ScheduleIntentPayload.ComputeHash("{\"a\":1}"), ScheduleIntentPayload.ComputeHash("{ \"a\":1}"));
        Assert.Equal("sha256-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("\"è😀\"")))
            .ToLowerInvariant(), ScheduleIntentPayload.ComputeHash("\"è😀\""));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("now")]
    [InlineData("update")]
    [InlineData("targets")]
    [InlineData("render")]
    [InlineData("correlation")]
    [InlineData("pharmacy")]
    [InlineData("edit")]
    [InlineData("group")]
    [InlineData("tags")]
    [InlineData("interval")]
    [InlineData("order")]
    [InlineData("shortlink")]
    public void Helper_rejects_side_effects_or_incomplete_schedule_envelopes(string variant)
    {
        var post = Post();
        var target = post.Posts[0];
        post = variant switch
        {
            "draft" => post with { Type = PostizPostType.Draft },
            "now" => post with { Type = PostizPostType.Now },
            "update" => post with { Type = PostizPostType.Update },
            "targets" => post with { Posts = [target, target] },
            "render" => post with { Posts = [target with { PrePublishRender = null }] },
            "correlation" => post with { Posts = [target with { PrePublishRender = new(null!) }] },
            "pharmacy" => post with { Posts = [target with
                { PrePublishRender = new(new("post-1", "snapshot-1", "group-a", null)) }] },
            "edit" => post with { Posts = [target with { Value = [new("edited", [], "existing-post")] }] },
            "group" => post with { Posts = [target with { Group = "existing-group" }] },
            "tags" => post with { Tags = [new("tag", "Tag")] },
            "interval" => post with { Inter = 1 },
            "order" => post with { Order = "1" },
            "shortlink" => post with { ShortLink = true },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<ArgumentException>(() => ScheduleIntentPayload.Create("cmd", "occ", "rev", "binding", post));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Helper_requires_stable_command_occurrence_revision_and_binding(int index)
    {
        var metadata = new[] { "cmd", "occ", "rev", "binding" };
        metadata[index] = " ";
        Assert.Throws<ArgumentException>(() => ScheduleIntentPayload.Create(
            metadata[0], metadata[1], metadata[2], metadata[3], Post()));
    }

    [Fact]
    public void Hash_bounds_utf8_bytes_and_rejects_invalid_unicode_without_normalization()
    {
        var boundary = new string('é', ScheduleIntentPayload.MaximumPayloadBytes / 2);
        Assert.Equal(71, ScheduleIntentPayload.ComputeHash(boundary).Length);
        Assert.Throws<ArgumentException>(() => ScheduleIntentPayload.ComputeHash(boundary + "a"));
        Assert.Throws<ArgumentException>(() => ScheduleIntentPayload.ComputeHash(""));
        Assert.Throws<ArgumentException>(() => ScheduleIntentPayload.ComputeHash("\ud800"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a?x")]
    [InlineData("a%2fb")]
    [InlineData("a b")]
    [InlineData("è")]
    [InlineData("a\nb")]
    public async Task Invalid_intent_tokens_never_reach_transport(string id)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAsync(id));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Maximum_ascii_token_is_valid_but_longer_token_is_not()
    {
        var id = new string('a', 126) + "_-";
        using var handler = new Handler((request, _) =>
        {
            Assert.EndsWith("/" + id, request.RequestUri!.AbsolutePath);
            return Task.FromResult(Receipt(id, Frozen()));
        });
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        Assert.Equal(id, (await client.GetAsync(id)).IntentId);
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAsync(id + "x"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Create_replay_lookup_and_cancel_preserve_frozen_payload_and_scope_headers()
    {
        var frozen = Frozen();
        var calls = new List<string>();
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("synthetic-org-key", request.Headers.GetValues("Authorization").Single());
            Assert.Equal("org-a", request.Headers.GetValues("X-HappyM-Organization-Id").Single());
            Assert.Equal("correlation-a", request.Headers.GetValues("X-Correlation-Id").Single());
            var path = request.RequestUri!.AbsolutePath;
            calls.Add(request.Method.Method + " " + path);
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("intent_A-1", request.Headers.GetValues("Idempotency-Key").Single());
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal(frozen.PayloadJson, body.RootElement.GetProperty("payloadJson").GetString());
                Assert.Equal(frozen.PayloadHash, body.RootElement.GetProperty("payloadHash").GetString());
                Assert.Equal(2, body.RootElement.EnumerateObject().Count());
            }
            else
            {
                Assert.Null(request.Content);
                Assert.False(request.Headers.Contains("Idempotency-Key"));
            }
            return Receipt("intent_A-1", frozen, path.EndsWith("/cancel", StringComparison.Ordinal));
        });
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        var created = await client.CreateAsync("intent_A-1", frozen);
        var replay = await client.CreateAsync("intent_A-1", frozen);
        // New SDK object receives the same stored strings; nothing is rebuilt from mutable business input.
        var lookup = await new PostizScheduleIntentsClient(http, Options()).GetAsync("intent_A-1");
        var cancelled = await client.CancelAsync("intent_A-1", frozen);
        Assert.Equal("committed", created.State);
        Assert.Equal("pending", created.DispatchState);
        Assert.Equal(created.PayloadHash, replay.PayloadHash);
        Assert.Equal(created.CommandId, lookup.CommandId);
        Assert.Equal("crm-occ-1", lookup.CrmOccurrenceId);
        Assert.Equal("revision-1", lookup.RevisionId);
        Assert.Equal("binding-a-test", lookup.BindingKey);
        Assert.Equal("org-a", lookup.OrganizationId);
        Assert.Equal("post-remote-1", Assert.Single(lookup.Links).PostId);
        Assert.Equal("occ-remote-1", Assert.Single(lookup.Links).OccurrenceId);
        Assert.Equal("cancelled", cancelled.State);
        Assert.Equal("cancelled", cancelled.DispatchState);
        Assert.Empty(cancelled.Links);
        var route = "/public/v1/prepublish-render/schedule-intents/intent_A-1";
        Assert.Equal(new[] { "POST " + route, "POST " + route, "GET " + route, "POST " + route + "/cancel" }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Frozen_hash_mismatch_is_rejected_before_create_or_cancel(bool cancel)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        var frozen = Frozen();
        frozen = frozen with { PayloadJson = frozen.PayloadJson + " " };
        await Assert.ThrowsAsync<ArgumentException>(() => cancel
            ? client.CancelAsync("intent-1", frozen) : client.CreateAsync("intent-1", frozen));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Writes_have_one_attempt_on_transient_response_or_lost_response(bool cancel, bool lostResponse)
    {
        using var handler = new Handler((_, _) =>
        {
            if (lostResponse) throw new HttpRequestException("Synthetic uncertain response.");
            var response = Json(HttpStatusCode.ServiceUnavailable, "{\"code\":\"schedule_intent_transient\"}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        });
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => cancel
            ? client.CancelAsync("intent-1", Frozen()) : client.CreateAsync("intent-1", Frozen()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Lookup_retries_read_transients_but_not_scoped_not_found()
    {
        var frozen = Frozen();
        var attempt = 0;
        using var handler = new Handler((_, _) =>
        {
            attempt++;
            if (attempt == 1)
            {
                var response = Json(HttpStatusCode.TooManyRequests, "{}");
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(response);
            }
            return Task.FromResult(attempt == 2 ? Receipt("intent-1", frozen)
                : Json(HttpStatusCode.NotFound, "{\"code\":\"schedule_intent_not_found\"}"));
        });
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        Assert.Equal("committed", (await client.GetAsync("intent-1")).State);
        Assert.Equal(2, handler.Calls);
        var error = await Assert.ThrowsAsync<PostizApiException>(() => client.GetAsync("intent-1"));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal("schedule_intent_not_found", error.ErrorCode);
        Assert.Equal(3, handler.Calls);
    }

    [Theory]
    [InlineData("schedule_intent_conflict")]
    [InlineData("schedule_intent_cancel_unsafe")]
    public async Task Structured_conflicts_preserve_existing_error_redaction(string code)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.Conflict,
            "{\"code\":\"" + code + "\",\"token\":\"do-not-return-token\",\"password\":\"do-not-return-password\"}")));
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        var error = await Assert.ThrowsAsync<PostizApiException>(() => client.CancelAsync("intent-1", Frozen()));
        Assert.Equal(code, error.ErrorCode);
        Assert.Equal(PostizApiReasonCode.Conflict, error.ReasonCode);
        Assert.DoesNotContain("do-not-return", error.ResponseBody);
        Assert.DoesNotContain("synthetic-org-key", error.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Precancelled_calls_do_not_reach_transport()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CreateAsync("intent-1", Frozen(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("intent-1", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CancelAsync("intent-1", Frozen(), cancellation.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Cancellation_during_transport_is_forwarded_without_write_replay()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new Handler((_, ct) =>
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation must have been forwarded.");
        });
        using var http = Http(handler);
        var client = new PostizScheduleIntentsClient(http, Options());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CreateAsync("intent-1", Frozen(), cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Legacy_public_interface_and_constructor_remain_usable_without_new_members()
    {
        Assert.DoesNotContain(typeof(IPostizClient).GetProperties(), p => p.PropertyType == typeof(IPostizScheduleIntentsClient));
        Assert.NotNull(typeof(PostizClient).GetConstructor([typeof(HttpClient), typeof(PostizOptions)]));
        Assert.NotNull(typeof(CreatePostRequest).GetConstructor([typeof(PostizPostType), typeof(DateTimeOffset),
            typeof(IReadOnlyList<PostizPostTarget>), typeof(bool), typeof(IReadOnlyList<PostizTag>),
            typeof(int?), typeof(string), typeof(string)]));
        // Reflection is only a source-shape regression assertion; old binary ABI is a separate Root gate.
    }

    private static ScheduleIntentRequest Frozen() => ScheduleIntentPayload.Create(
        "command-1", "crm-occ-1", "revision-1", "binding-a-test", Post());

    private static CreatePostRequest Post() => new(PostizPostType.Schedule, Date,
        [new(new("integration-a"), [new("caption è", []), new("comment", [], Delay: 2)],
            JsonSerializer.SerializeToElement(new { post_type = "post" }),
            PrePublishRender: new(new("post-1", "snapshot-1", "group-a", "pharmacy-a"))) ]);

    private static PostizOptions Options() => new()
    {
        BaseAddress = new Uri("https://postiz.invalid/"), ApiKey = "synthetic-org-key",
        OrganizationId = "org-a", MaxRetryAttempts = 2, CorrelationIdFactory = () => "correlation-a"
    };

    private static HttpClient Http(Handler handler) => new(handler) { BaseAddress = new Uri("https://postiz.invalid/") };

    private static HttpResponseMessage Receipt(string id, ScheduleIntentRequest frozen, bool cancelled = false) =>
        Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            contract = ScheduleIntentPayload.Contract, intentId = id, payloadHash = frozen.PayloadHash,
            commandId = "command-1", crmOccurrenceId = "crm-occ-1", revisionId = "revision-1",
            bindingKey = "binding-a-test", organizationId = "org-a", state = cancelled ? "cancelled" : "committed",
            dispatchState = cancelled ? "cancelled" : "pending", links = cancelled ? Array.Empty<ScheduleIntentLink>()
                : [new ScheduleIntentLink("integration-a", "post-remote-1", "occ-remote-1")]
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return callback(request, cancellationToken);
        }
    }
}
