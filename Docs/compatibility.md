# Compatibility

| Postiz.NET | Postiz | Contract |
|---|---|---|
| 1.0.0-alpha.1 | v2.23.0 | `/public/v1`, fixture contract tests |
| 1.0.0-alpha.2 | HappyM.Postiz / v2.23.0 | complete HappyM public integration surface, provider catalog and signed webhooks |
| 1.0.0-alpha.11 | HappyM.Postiz / v2.23.0 | native org-scoped media library and operational AI chat |

The alpha is not declared compatible with `latest`. Each new Postiz baseline
must run fixture tests and opt-in integration tests against the pinned image.
Stable `1.0.0` is gated on the missing webhook/version contracts and the HappyM
two-pharmacy end-to-end scenario.

## Unreleased schedule-intent source candidate

The proposed local version `1.0.0-beta.8.editorial.intent.<commit12>` adds a
standalone typed client for `editorial.schedule-intent/v1`; it is not built or
qualified. The frozen beta8 appliance does not expose this capability. Require
the separately qualified engine successor, explicit enablement and matching
SDK/Extension consumer pins; no compatibility with the old image is inferred.
Legacy interfaces, constructors, DTOs and transport remain unchanged. See
[schedule intents](schedule-intents.md) for the frozen payload and recovery rules.
