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
