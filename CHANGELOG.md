# Changelog

## 1.0.0-alpha.11 — 2026-08-13

- Aggiunti `IPostizMediaClient.ListAsync` e `DeleteAsync`, inclusi `createdAt`, ricerca e paginazione org-scoped.
- Aggiunto `IPostizChatClient` per invio messaggi e lettura thread IA tramite Public API M2M.
- Conservati upload esistenti, header farmacia e classificazione strutturata `PostizApiException`.

## 1.0.0-alpha.10 — 2026-08-11

- `PostizApiException` espone `StatusCode`, `ErrorCode`, `IsTransient` e `ReasonCode` tipizzati.
- Il messaggio tecnico usa il branding Social Manager e il body resta sanificato.
- Aggiunta la classificazione esplicita di gateway, autorizzazione, rate limit ed errori server.

## 1.0.0-alpha.7 — 2026-08-09

- Aggiunti stato provider e hot-apply M2M dell'app OAuth Facebook/Instagram.
- Il secret è write-only: assente dai DTO di stato e dalle risposte SDK.
- Supportato l'update dell'App Id mantenendo il secret già applicato.

## 1.0.0-alpha.6 — 2026-08-09

- Release coordinata con l'invariante no-login del Connect OAuth.
- Contratti SDK invariati rispetto ad alpha.5.

## 1.0.0-alpha.5 — 2026-08-08

- Release coordinata con il fix dell'exchange Connect Social Manager.
- Contratti SDK invariati rispetto ad alpha.4.

## 1.0.0-alpha.4 — 2026-08-08

- Release coordinata con il Connect OAuth presidiato dell'estensione HappyM.
- Compatibilità dichiarata con le connect-session purpose/provider alpha.4.

## 1.0.0-alpha.3 — 2026-08-08

- Client tipizzato `IPostizApplianceClient` per bootstrap e controllo M2M.
- Supporto alla rotazione della chiave di servizio e allo scope farmacia con `X-HappyM-Organization-Id`.
- Autenticazione separata fra API key pubblica e credenziali interne.

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
