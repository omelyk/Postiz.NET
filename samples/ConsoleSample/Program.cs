using Postiz;

var baseAddress = Environment.GetEnvironmentVariable("POSTIZ_URL")
    ?? throw new InvalidOperationException("Set POSTIZ_URL.");
var apiKey = Environment.GetEnvironmentVariable("POSTIZ_API_KEY")
    ?? throw new InvalidOperationException("Set POSTIZ_API_KEY.");

using var httpClient = new HttpClient();
var client = new PostizClient(httpClient, new PostizOptions
{
    BaseAddress = new Uri(baseAddress),
    ApiKey = apiKey,
});

foreach (var integration in await client.Integrations.GetAsync())
{
    Console.WriteLine($"{integration.Name}: {integration.Identifier} ({integration.Id})");
}
