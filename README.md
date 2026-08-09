# Postiz.NET

Typed and testable .NET SDK for the self-hosted Postiz Public API. Version
`1.0.0-alpha.6` targets .NET 8 and .NET 9 and is compatible with the HappyM.Postiz
fork based on upstream `v2.23.0`.

The SDK is independent from HappyM: it contains no CRM rules, tenant mappings or
database access. HappyM.Pharma integrations consume it like any other .NET
application.

## Packages

- `Postiz.NET`: transport, DTOs and domain clients;
- `Postiz.NET.DependencyInjection`: `HttpClientFactory` registration;
- `Postiz.NET.AspNetCore`: Postiz health check.

## Quick start

```csharp
services.AddPostizClient(options =>
{
    options.BaseAddress = new Uri(configuration["Postiz:Url"]!);
    options.ApiKey = configuration["Postiz:ApiKey"]!;
});
```

```csharp
public sealed class ChannelReader(IPostizClient postiz)
{
    public Task<IReadOnlyList<PostizIntegration>> ReadAsync(
        CancellationToken cancellationToken) =>
        postiz.Integrations.GetAsync(cancellationToken: cancellationToken);
}
```

The API key is sent in the raw `Authorization` header, matching Postiz's Public
API. Never place it in source control or logs.

## Build

```powershell
dotnet restore Postiz.NET.slnx --configfile NuGet.config
dotnet test Postiz.NET.slnx --configuration Release --no-restore
dotnet pack Postiz.NET.slnx --configuration Release --no-restore
```

See [API coverage](Docs/api-coverage.md), [authentication](Docs/authentication.md),
[errors and retries](Docs/errors-retries.md), and
[compatibility](Docs/compatibility.md).
