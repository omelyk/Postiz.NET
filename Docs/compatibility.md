# Current producer and consumer contract

The new product uses one current SDK/appliance/Extension contract. Historical
interfaces, constructors, binary ABI and demonstration seeds are not compatibility
requirements. SDK and consumers advance together; no compatibility adapter or
standalone schedule client is maintained for older callers.

The proposed local source version `1.0.0-beta.8.editorial.intent.<commit12>` exposes
`IPostizClient.ScheduleIntents` for `editorial.schedule-intent/v1`. It is not built
or qualified. The current appliance successor and consumer pins must be qualified
together before enablement. An old beta8 image does not implement this contract
and is not an alternative execution path.

Qualify the current wire contract, authentication, receipts, cancellation,
transactional behavior and two-replica scenario on the assigned target. These
are product correctness checks, not historical compatibility gates. See
[schedule intents](schedule-intents.md) for payload and recovery rules.
