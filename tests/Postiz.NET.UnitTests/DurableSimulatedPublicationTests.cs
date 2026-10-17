using System.Net;
using System.Text;
using System.Text.Json;
using Postiz;
using Xunit;

namespace Postiz.NET.UnitTests;

// Explicit transport fixture: no network, social account, or appliance is used.
public sealed class DurableSimulatedPostMutationTests
{
    [Fact]
    public async Task Lost_schedule_response_is_not_replayed_and_existing_post_readback_survives_transport_recreation()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), ".tmpwork", "finalizzazione-editoriale",
            "simulated-publication", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var receiptPath = Path.Combine(directory, "synthetic-receipt.json");
        await File.WriteAllTextAsync(receiptPath, """{"id":"sim-post-1","organizationId":"org-a","releaseId":"sim-release-1","state":"DRAFT"}""");
        using (var writer = new SimulatedHandler(receiptPath))
        using (var http = new HttpClient(writer) { BaseAddress = new Uri("https://postiz.test/") })
        {
            var client = Client(http, "crm-a-key", "org-a");
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.Posts.ChangeStatusAsync("sim-post-1", "schedule"));
            Assert.Equal(1, writer.WriteCalls);
        }

        // A newly created handler reads only the committed file; the original instance is gone.
        using var reader = new SimulatedHandler(receiptPath);
        using var readHttp = new HttpClient(reader) { BaseAddress = new Uri("https://postiz.test/") };
        var receipt = await Client(readHttp, "crm-a-key", "org-a").Posts.GetByReleaseIdAsync("sim-release-1");
        Assert.Equal("QUEUE", receipt.GetProperty("state").GetString());
        Assert.Equal("sim-release-1", receipt.GetProperty("releaseId").GetString());
        Assert.Equal("sim-post-1", receipt.GetProperty("id").GetString());
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

            if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath == "/public/v1/posts/sim-post-1/status")
            {
                WriteCalls++;
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal("schedule", body.RootElement.GetProperty("status").GetString());
                var receipt = JsonSerializer.Serialize(new
                {
                    id = "sim-post-1", organizationId = organization, releaseId = "sim-release-1", state = "QUEUE",
                });
                // A known pre-existing post changes to QUEUE before the simulated response is lost.
                // This does not simulate provider publication, Temporal, or a YouTube registry.
                await using (var file = new FileStream(receiptPath, FileMode.Create, FileAccess.Write,
                                 FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    await file.WriteAsync(Encoding.UTF8.GetBytes(receipt), cancellationToken);
                    file.Flush(flushToDisk: true);
                }
                throw new HttpRequestException("Synthetic response loss after simulated scheduling commit.");
            }

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/public/v1/posts/by-release-id/sim-release-1")
            {
                if (!File.Exists(receiptPath)) return Json(HttpStatusCode.NotFound, "{}");
                var receipt = await File.ReadAllTextAsync(receiptPath, cancellationToken);
                using var document = JsonDocument.Parse(receipt);
                return document.RootElement.GetProperty("organizationId").GetString() == organization
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
