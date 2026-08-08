# Changelog

## 1.0.0-alpha.2 - Unreleased

- Completed the HappyM `/public/v1` integration surface with capability and
  provider discovery, OAuth connect URLs, channel settings/deletion,
  notifications, video functions, release-ID reconciliation and webhook CRUD.
- Added the signed webhook contract used by HappyM consumers.
- Kept provider-specific post options schema-driven so every provider registered
  by the pinned fork is supported without coupling the SDK to upstream DTOs.

## 1.0.0-alpha.1 - 2026-08-08

- Added multi-targeted .NET 8/9 SDK projects and NuGet metadata.
- Added authenticated transport, cancellation, typed redacted errors and safe
  transient retries.
- Added groups, integrations/settings/tools, media, posts, analytics and health
  clients for Postiz v2.23.0.
- Added `HttpClientFactory`, ASP.NET Core health check, samples and automated
  unit/contract/integration tests.
