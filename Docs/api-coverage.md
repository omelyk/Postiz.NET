# API coverage

Supported by `Postiz.NET 1.0.0-alpha.2` against HappyM.Postiz based on upstream
`v2.23.0` `/public/v1`:

| Area | Routes |
|---|---|
| Compatibility | product/API/upstream/fork version and capability discovery |
| Health | `GET is-connected` |
| Groups | `GET groups` |
| Providers | catalog of every provider registered by the pinned fork and OAuth connect URL |
| Integrations | list, settings/schema, update settings, provider tool trigger, delete channel |
| Media | multipart upload, upload from public URL, video generation and provider functions |
| Posts | list, create draft/now/schedule/update, find slot, missing content, status, release-id reconciliation, delete post/group |
| Analytics | integration and post analytics |
| Notifications | paginated organization notifications |
| Webhooks | list/create/update/delete plus HMAC SHA-256 delivery contract |

The provider catalog is generic by design: all provider-specific post settings
are passed through `PostizPostTarget.Settings` as JSON and can be discovered via
`GetSettingsAsync`. This covers every provider registered by the pinned fork
without hard-coding social DTOs that change independently upstream.

The SDK intentionally does not expose dashboard-internal APIs such as billing,
team administration, Copilot, announcements or browser session management.
Those are not part of the HappyM integration boundary.

Some providers require an external URL, a browser extension, Web3 signing or
manual action in their native app. The catalog reports those flags; SDK support
does not remove the provider's own onboarding or platform restrictions.
