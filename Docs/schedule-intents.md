# Schedule intents — Source candidate only

`editorial.schedule-intent/v1` is an additive, uncompiled source candidate. The
frozen beta8 appliance does not implement this contract. No package/image or
runtime compatibility is qualified by these files. Capability enablement,
schema, deployment, and any real integration test remain explicit release gates.

Use `Postiz.ScheduleIntents.IPostizScheduleIntentsClient` through the standalone
`PostizScheduleIntentsClient(HttpClient, PostizOptions)`. `IPostizClient`,
`PostizClient`, legacy DTO constructors, DI registrations and transport are
unchanged. This is a scheduling receipt; it never establishes that a social
publication occurred.

## Freeze once

`ScheduleIntentPayload.Create(commandId, crmOccurrenceId, revisionId, bindingKey,
CreatePostRequest)` returns `ScheduleIntentRequest(PayloadJson, PayloadHash)`.
The helper accepts one schedule target with a pre-publish-render configuration
and complete post/snapshot/group/pharmacy correlation. Edit IDs/groups, NOW,
draft/update, tags, interval, order and short-link side effects are rejected.
The root value may be followed by comments. Existing server post validation
and comment normalization remain authoritative.

The envelope contains contract, commandId, crmOccurrenceId, revisionId,
bindingKey and post. Post uses the existing Posts.Create wire projection and
serializer: lowercase type, offset date in `O` format, target values and
legacy null omission. Organization is never added to the envelope. BindingKey
must come from server configuration, rather than browser input.

`PayloadHash` is `sha256-` plus 64 lowercase hex digits over the exact UTF-8
payload string. No trimming, JSON canonicalization or reconstruction is applied.
Invalid Unicode and strings outside 1..262144 UTF-8 bytes are rejected.
`ComputeHash` hashes raw text; it does not assert that arbitrary text is a valid
schedule envelope. The helper builds the envelope; the server validates it.

The caller must durably store **both returned strings before any write** and
reuse them for create/cancel recovery. Client calls verify size/hash but treat
the frozen payload as opaque. Mutating a business row and invoking the helper
again is a new payload, potentially a conflict, not recovery of the old intent.
This SDK has no journal, database or background reconciliation loop.

## Scoped routes and outcomes

Intent IDs are ASCII `[A-Za-z0-9_-]{1,128}`; invalid IDs never reach transport.
Routes are relative to the configured appliance base address:

- `CreateAsync(id, frozen, ct)` sends POST
  `public/v1/prepublish-render/schedule-intents/{id}`.
- `GetAsync(id, ct)` sends GET to that exact route; it does not scan post lists.
- `CancelAsync(id, frozen, ct)` sends POST to that route plus `/cancel`, with
  the same frozen request. It can establish a server-side cancellation tombstone
  before a delayed create arrives.

Writes include `Idempotency-Key: id` and use one HTTP attempt, including after
503, Retry-After or an uncertain transport failure. Organization plus URL intent
ID and the server-persisted payload hash form the authoritative key. An HTTP
header alone does not establish server idempotency. GET retains the existing
bounded read retry policy. A lookup 404 stays `PostizApiException` with
`schedule_intent_not_found`; it never triggers create or a legacy fallback.

The existing raw Authorization, optional X-HappyM-Organization-Id, correlation,
cancellation, request timeout and redacted error transport are reused. API keys
are private backend inputs; this client does not use browser cookies or OAuth
provider credentials. Consumers must require the server capability and verify
receipt metadata against their frozen intent and bound organization.

Receipt fields: contract, intentId, payloadHash, commandId, crmOccurrenceId,
revisionId, bindingKey, organizationId, state, dispatchState, links. Each link
contains integrationId, postId, occurrenceId. States are `committed` and
`cancelled`; dispatchState is `pending`, `started` or `cancelled`. A cancelled
tombstone can have no links. Published/Publishing cancellation safety and
transactional server replay are engine responsibilities, not SDK inferences.

Structured server codes remain in `PostizApiException.ErrorCode` with the
existing HTTP reason mapping: schedule_intent_invalid (400),
schedule_intent_not_found (404), schedule_intent_conflict and
schedule_intent_cancel_unsafe (409), schedule_intents_disabled and
schedule_intent_transient (503). No new legacy enum member is required.

## Validation and version plan

`ScheduleIntentTests` contains in-memory HttpMessageHandler fixtures for wire
parity, hash/UTF-8 limits, validation, routing/scope headers, DTO replay/tombstone,
write single-attempt, lookup retry/404, error redaction and cancellation. They
are prepared source only: no compiler, syntax runner, tests or server were run.
Fixture replay is not proof of a server transaction, SQL concurrency, restart,
cross-process A1/A2, dispatch, provider execution or historical binary ABI.

Proposed local version after source commit: `1.0.0-beta.8.editorial.intent.<commit12>`.
Directory.Build.props remains beta8; nothing is built, packed or published here.
Root must qualify producer/consumer pins and the new appliance capability before
adoption. Existing beta8 clients and their retry behavior remain unchanged.
