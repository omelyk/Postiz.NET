# Errors and retries

Non-success responses throw `PostizApiException` with HTTP status, `ErrorCode`,
`IsTransient`, a typed `ReasonCode`, and a safe error
code when present, `X-Correlation-Id` and a redacted/truncated JSON body.
Authorization, token, secret, password and media fields are removed.

The SDK retries only idempotent reads/deletes for `408`, `429` and `5xx`, using
`Retry-After` when supplied and capped exponential delay otherwise. Create,
upload, trigger and update operations are never retried because Postiz v2.23.0
does not expose an idempotency-key guarantee. HappyM must reconcile uncertain
write outcomes through its publication registry.

Every async API accepts a `CancellationToken`; the configured request timeout is
linked to it.

Both delta-seconds and HTTP-date forms of `Retry-After` are supported. Expired
dates permit an immediate retry; server delays retain the existing 30-second
cap and the total request cancellation deadline. This is a per-request policy,
not a distributed appliance quota across CRM instances. Non-idempotent writes
remain single-attempt even when a response contains `Retry-After`.
