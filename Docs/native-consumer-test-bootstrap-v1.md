# Native consumer bootstrap for local TEST

`PostizNativeConsumerClient` is a separate native provisioning client. It does
not extend `IPostizClient`, the ordinary runtime factory, a browser API or PROD
onboarding. The host must retain the real selected owner27/lease/CAS, installed
database, MultiCrm scope and TargetVerified binding before invoking it. The SDK
does not manufacture any of that authority.

The new engine route is `POST /internal/happym/appliance/native-consumer/bootstrap`.
The exact body is `binding`, `consumer`, `requestId`, `requireExisting`:

- `binding`: clientId, applicationEnvironmentId, logicalDeploymentId, installationId;
- `consumer`: deploymentId, environment, tenantId, pharmacyId;
- `deploymentId` must equal the selected logicalDeploymentId, `environment` is
  exactly `test`, and deploymentId matches `[a-z0-9-]{1,64}`;
- tenantId and pharmacyId match `[A-Za-z0-9_.:-]{1,100}`. This safe subset gives
  the same JSON-array UTF-8 identity hash in .NET and Node without changing the
  existing CRM identity codec;
- `requireExisting=true` rejects any missing organization, user or membership.
  A previous successful replay must not silently recreate deleted remote state.

Existing M2M authentication remains mandatory. Both this QA-only bootstrap and
the separate fixture provision call additionally require the protected native
TEST provisioning purpose header `x-happym-native-fixture-authorization`. It
uses the existing `HAPPYM_TEST_FIXTURE_NATIVE_SECRET` purpose, distinct from the
M2M/control and embed credentials, and only with the existing TEST/safety flags.
It is not an authorization or secret contract for ordinary PROD installations.

The engine derives immutable organization/user IDs from the selected consumer
scope, uses a serializable Prisma transaction, creates only missing rows and
checks every existing identity, active state and membership role. Exact replay
preserves the native organization key. Any disagreement fails closed; the old
mutating `ensureUser`, SYSTEM-key fallback, implicit rotation and role updates
are not used.

The response contains a credential-bearing **internal wire envelope**, consumed
only in RAM by the native receiver. The SDK has no public credential response
DTO. `BootstrapNativeConsumerAsync` passes borrowed character memory to
`IPostizNativeCredentialSink.ReceiveAsync` only after exact scope/contract checks.
The protected host may copy it into a private phase-owned KeyRing, then must
clear/dispose that holder when the native phase ends. The returned receipt has
only public binding, organization/user and request metadata. No credential,
credential hash, inner exception or response body is returned/logged/serialized.
Owned receive buffers are cleared; immutable runtime strings are not claimed to
be physically erased.

The caller supplies the qualified native loopback HTTP transport with DNS/address
binding, redirects/proxy/cookies disabled and normal TLS validation. HTTP is only
the host's explicitly selected private loopback `AllowPrivateHttpApi` contract.
The SDK caps this operation at 10 seconds and 64 KiB, does not follow a different
response origin, rejects duplicate/extra/mismatching JSON fields, and never
retries. After dispatch, any failure (including sink commit/cancellation) becomes
the fixed `PostizNativeOutcomeUnknownException`, without an inner exception or
secret-bearing response body. The owner retains its durable UNKNOWN barrier and
must obtain a new finite assignment for readback/reconciliation.

`ProvisionNativeFixtureAsync` reuses the existing
`POST /internal/happym/appliance/test-fixture/provision` with its exact
`{binding,organizationId,profile}` body. It accepts only the four existing
synthetic profiles and verifies contract/mode/provider/label and the integration
ID derived from the exact binding, organization and profile. A provision response
alone is insufficient to link CRM state: the host must independently read the
actual organization-scoped integrations using the scoped native key. The
existing `PostizClient.Integrations.GetAsync` uses raw `Authorization` and no
organization override for that non-SYSTEM key.

This Source delta is serial after the qualified ep9/v3 source manifest
`6CB646427DA04231ECC5514534A094CC774A258C97443175E313912B423FA8B3`
and Current06/v3 engine composition. The already qualified beta9 package does
not contain these additional files. A new immutable SDK recipe/package and
paired engine build, independent peer and real native tests remain required.
General PROD first bootstrap remains an explicit open gap. No native, UAT,
deployment or release qualification is supplied by deterministic Source tests.
