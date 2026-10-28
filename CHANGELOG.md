# Changelog

## Unreleased � occurrence-scoped synthetic receipt

- Added a read-only organization-scoped receipt call to the current prepublish-render client, reusing the synthetic receipt DTO and validation.
- Kept control-plane credentials and fixture authorization separate from operational reads.


## Native TEST bootstrap - additional Source delta, not released

- Added a separate native TEST consumer bootstrap client with a protected RAM credential receiver and a public receipt containing no key. Ordinary clients/factories remain unchanged.
- Added explicit first-write/exact and require-existing replay semantics; the paired engine never silently reactivates users, changes roles, rewrites scoped keys or falls back to SYSTEM credentials.
- Added native fixture provision calls using the existing TEST-purpose contract, exact synthetic IDs and no uncertain-write retry. PROD first bootstrap remains unimplemented.
- Additional deterministic Source tests are prepared only. This delta requires a new immutable SDK package and paired engine qualification; prior beta9 package PASS does not qualify these additions.

## 1.0.0-beta.9 — Source candidate, not released

- Breaking current attach contract `prepublish-render/v3`: required target contentMode text_only/media contributes to the canonical digest; existing Public API route and lease/idempotency protocol remain.
- Text-only requires exactly one caption target and no media/thumbnail/story sequence. Media retains1..10 plus existing Story guards.
- Provider settings/content/media validation is shared with ordinary post validation and runs before Ready inside the serializable attach transaction; no Temporal workflow or activity ABI changes.
- Source tests/goldens added; no build, tests, package/image release or runtime/UAT qualification claimed by this Source delivery.

## Unreleased

- Integrated `ScheduleIntents` into the current `IPostizClient` with frozen UTF-8 payload hashing and typed create/lookup/cancel receipts. Removed the standalone public client and historical signature-preservation requirement; consumers advance with this contract. Source fixtures are not compiled or executed, and the current appliance contract is not qualified.
- Honor HTTP-date `Retry-After` responses on idempotent requests, including expired dates and cancellation during the wait. Preserve the existing delay cap and single-attempt write boundary.

## 1.0.0-beta.8 — 2026-09-02

- Added the additive `prepublish-render/v2` Story-sequence contract through `RenderPublishModes.StorySequence` and `RenderTarget.PublishMode`.
- Added typed bundle and child receipts on `RenderOccurrence.PublishReceipt`, preserving ordered slide IDs and provider URLs for CRM audit.
- Added structured `StorySequenceInvalid` and `StorySequenceUnsupported` reason codes.
- Kept all `prepublish-render/v1` calls and one-media Story payloads source- and wire-compatible.

## 1.0.0-beta.7 — 2026-08-31

Post-Settings schema: Warning
- Added typed discovery for the versioned `post-comments/v1` contract returned by `GetSettingsAsync`.
- Added stable `PostizPostSettingKeys.FirstComment`, `Comments`, and `ValidUntil` constants while preserving raw `Output` compatibility.
- Documented `firstComment` as a string, ordered `comments` items, native `delay` minutes, and the distinction between public post comments and inbox chat.
- Joint HappyM.Pharma Instagram/Facebook publication UAT remains required before production promotion.

## 1.0.0-beta.6 — 2026-08-31

Studio-Render-WebM-UAT-Fail: Warning
- Coordinated the private SDK release with the Social Manager appliance runtime and local-media resolution fix.
- The native YouTube publish contract and semantic reason codes are unchanged; no consumer source changes are required.
- Joint HappyM.Pharma unlisted YouTube UAT remains required before production promotion.

## 1.0.0-beta.5 — 2026-08-26

PrePublish-Render-Hook: Warning
- Contract version: `prepublish-render/v1`
- Breaking: no
- Gate enforce: yes
- Added typed schedule correlation, occurrence list/get/cancel, idempotent claim/attach, canonical content-hash generation, and structured render reason codes.
- Test evidence: 25 unit tests per target framework plus contract/integration suites on .NET 8 and .NET 9.

## 1.0.0-beta.4 — 2026-08-22

- Aligned the default native YouTube publish timeout to 30 minutes so appliance-side WebM normalization and resumable upload share the same operational window.

## 1.0.0-beta.3 — 2026-08-22

- Added semantic `MediaTranscodeFailed` and `MediaFormatUnsupported` reason codes for Media Studio WebM publishing through the Social Manager appliance.

## 1.0.0-beta.2 — 2026-08-22

- Added the typed `IPostizPostsClient.PublishYoutubeAsync` native publishing contract for tenant-owned videos and optional custom thumbnails.
- Added semantic API reason codes for missing video, insufficient YouTube scopes, rejected thumbnails and missing thumbnail scope.
- Added a dedicated configurable ten-minute YouTube publish timeout while preserving non-retry semantics for the mutation.

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
