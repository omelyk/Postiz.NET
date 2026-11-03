# Compatibility

## Stable cohort 3.1.0 - release preparation

- SDK packages: `Postiz.NET`, `Postiz.NET.DependencyInjection` and `Postiz.NET.AspNetCore`, all `3.1.0`, on `net8.0` and `net9.0`. Functional source is the qualified SDK cohort already merged by PR #2, unchanged by this version promotion.
- Private host adapters use an exact `[3.1.0]` SDK pin. Their package versions and Vue adapter are recorded separately in the admitted release matrix; a stable SDK version does not qualify an adapter or appliance automatically.
- The appliance keeps the existing upstream `2.23.0` baseline and the existing Public API/render contracts. Select it by the admitted immutable image digest; never infer compatibility from `latest` or an upstream tag alone.
- Stable package CI, package publication and cloud adoption remain separate evidence. Native TEST bootstrap stays TEST-only; a PROD first bootstrap is not implemented by this version promotion.


| Postiz.NET | Postiz | Contract |
|---|---|---|
| 1.0.0-alpha.1 | v2.23.0 | `/public/v1`, fixture contract tests |
| 1.0.0-alpha.2 | HappyM.Postiz / v2.23.0 | complete HappyM public integration surface, provider catalog and signed webhooks |
| 1.0.0-alpha.11 | HappyM.Postiz / v2.23.0 | native org-scoped media library and operational AI chat |

The alpha is not declared compatible with `latest`. Each new Postiz baseline
must run fixture tests and opt-in integration tests against the pinned image.
Stable `1.0.0` is gated on the missing webhook/version contracts and the HappyM
two-pharmacy end-to-end scenario.

## Current Source candidate beta.9

SDK and engine must be qualified together on prepublish-render/v3. contentMode is mandatory; route /public/v1/prepublish-render/occurrences remains. No v1/v2 attach fallback is supplied. beta.9 here is a Source version, not a published NuGet/image pin or live compatibility claim. Contract tests net8/net9, engine suites and a pinned synthetic-provider integration remain for Root/owners.

## Additional native TEST bootstrap Source delta

The new standalone PostizNativeConsumerClient and happym.native-consumer-bootstrap/v1 require a new paired SDK/engine build after the qualified ep9/v3 source. Existing beta9 package pins do not contain this increment and must not be reused as proof. This contract is local TEST only, requires the native owner plus separate TEST-purpose credential and does not qualify general PROD first bootstrap. See native-consumer-test-bootstrap-v1.md.

## Current Source: occurrence-scoped synthetic receipt read

`PrePublishRender.GetSyntheticDeliveryReceiptAsync(occurrenceId, expectedPostId, expectedOrganizationId)` reads
`GET /public/v1/prepublish-render/occurrences/:id/synthetic-receipt` through the existing
organization API-key transport. The API key determines the authenticated organization;
MultiCrm intentionally leaves `PostizOptions.OrganizationId` null, so no organization header
is added. The server derives the post from the scoped occurrence. Expected post and
organization IDs are local response checks only, never request overrides. If an existing
administrative data-plane client configures `OrganizationId`, it must match the expected
organization before any request. This reuses `happym.synthetic-delivery/v1`, its existing DTO and strict
journal validation. It does not call the M2M test-fixture route or fall back to it.
The paired engine remains based on Postiz v2.23.0. This is Source only: a new SDK package,
engine route qualification and current consumer rebuild are required before runtime use.
