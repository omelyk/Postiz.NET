using System.Net;
using System.Text;
using System.Text.Json;
using Postiz;
using Postiz.Posts;
using Xunit;

namespace Postiz.NET.UnitTests;

// Explicit transport fixture: no network, social account, or appliance is used.
public sealed class DurableSimulatedPublicationTests
{
    [Fact]
    public async Task Lost_write_response_is_not_replayed_and_durable_receipt_survives_transport_recreation()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), ".tmpwork", "finalizzazione-editoriale",
            "simulated-publication", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var receiptPath = Path.Combine(directory, "synthetic-receipt.json");
        using (var writer = new SimulatedHandler(receiptPath))
        using (var http = new HttpClient(writer) { BaseAddress = new Uri("https://postiz.test/") })
        {
            var client = Client(http, "crm-a-key", "org-a");
            await Assert.ThrowsAsync<HttpRequestException>(() => client.Posts.PublishYoutubeAsync(
                new PublishYoutubeRequest("simulated-channel", "Synthetic publication", YoutubeFormatHints.Video,
                    VideoMediaId: "synthetic-video")));
            Assert.Equal(1, writer.WriteCalls);
        }

        // A newly created handler reads only the committed file; the original instance is gone.
        using var reader = new SimulatedHandler(receiptPath);
        using var readHttp = new HttpClient(reader) { BaseAddress = new Uri("https://postiz.test/") };
        var receipt = await Client(readHttp, "crm-a-key", "org-a").Posts.GetByReleaseIdAsync("sim-release-1");
        Assert.Equal("Published", receipt.GetProperty("status").GetString());
        Assert.Equal("sim-release-1", receipt.GetProperty("releaseId").GetString());
        Assert.True(receipt.GetProperty("simulated").GetBoolean());
        Assert.Equal(0, reader.WriteCalls);

        var forbidden = await Assert.ThrowsAsync<PostizApiException>(() =>
            Client(readHttp, "crm-b-key", "org-a").Posts.GetByReleaseIdAsync("sim-release-1"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var otherOrganization = await Assert.ThrowsAsync<PostizApiException>(() =>
            Client(readHttp, "crm-b-key", "org-b").Posts.GetByReleaseIdAsync("sim-release-1"));
        Assert.Equal(HttpStatusCode.NotFound, otherOrganization.StatusCode);
    }

    private static PostizClient Client(HttpClient http, string key, string organization) => new(http,
        new PostizOptions
        {
            BaseAddress = http.BaseAddress!, ApiKey = key, OrganizationId = organization, MaxRetryAttempts = 2,
        });

    private sealed class SimulatedHandler(string receiptPath) : HttpMessageHandler
    {
        public int WriteCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.Headers.GetValues("Authorization").Single();
            var organization = request.Headers.GetValues("X-HappyM-Organization-Id").Single();
            var boundOrganization = key switch { "crm-a-key" => "org-a", "crm-b-key" => "org-b", _ => null };
            if (boundOrganization is null || organization != boundOrganization)
                return Json(HttpStatusCode.Forbidden, "{}");

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/public/v1/posts/youtube/publish")
            {
                WriteCalls++;
                var receipt = JsonSerializer.Serialize(new
                {
                    organization, releaseId = "sim-release-1", status = "Published", simulated = true,
                    channel = "simulated-channel",
                });
                // The simulated provider's result is durably closed before the response is lost.
                await using (var file = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    await file.WriteAsync(Encoding.UTF8.GetBytes(receipt), cancellationToken);
                    file.Flush(flushToDisk: true);
                }
                throw new HttpRequestException("Synthetic response loss after simulated provider commit.");
            }

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/public/v1/posts/by-release-id/sim-release-1")
            {
                if (!File.Exists(receiptPath)) return Json(HttpStatusCode.NotFound, "{}");
                var receipt = await File.ReadAllTextAsync(receiptPath, cancellationToken);
                using var document = JsonDocument.Parse(receipt);
                return document.RootElement.GetProperty("organization").GetString() == organization
                    ? Json(HttpStatusCode.OK, receipt)
                    : Json(HttpStatusCode.NotFound, "{}");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}
