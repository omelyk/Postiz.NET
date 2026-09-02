# API coverage

Supported by `Postiz.NET 1.0.0-beta.8` against HappyM.Postiz based on upstream
`v2.23.0` `/public/v1`:

| Area | Routes |
|---|---|
| Compatibility | product/API/upstream/fork version and capability discovery |
| Health | `GET is-connected` |
| Groups | `GET groups` |
| Providers | catalog of every provider registered by the pinned fork and OAuth connect URL |
| Integrations | list, settings/schema, update settings, provider tool trigger, delete channel |
| Media | organization-scoped list/delete, multipart upload, upload from public URL, video generation and provider functions |
| AI chat | send an operational message and read an organization-scoped thread |
| Posts | list, create draft/now/schedule/update, find slot, missing content, status, release-id reconciliation, delete post/group |
| Analytics | integration and post analytics |
| Notifications | paginated organization notifications |
| Webhooks | list/create/update/delete plus HMAC SHA-256 delivery contract |

The provider catalog is generic by design: all provider-specific post settings
are passed through `PostizPostTarget.Settings` as JSON and can be discovered via
`GetSettingsAsync`. This covers every provider registered by the pinned fork
without hard-coding social DTOs that change independently upstream.

Public comments use the versioned `post-comments/v1` contract returned as
`GetSettingsAsync(...).Output.postComments`. For providers where `supported` is
`true`, `Settings.firstComment` is a string containing the first public comment
below the post. `Settings.comments` is an ordered array whose items are strings
or `{ "content": "...", "delay": 5 }` objects; `delay` is an optional
non-negative number of minutes after the previous item. The API normalizes those
keys to native `posts[].value[1..]` entries before validation and publication.
`PostizPostSettingKeys` exposes the exact stable key names to .NET consumers.
This is unrelated to inbox/chat replies. `validUntil` remains consumer metadata
and is not a publishing-window setting in the Social Manager engine.

The SDK intentionally does not expose dashboard-internal APIs such as billing,
team administration, CopilotKit transport, announcements or browser session management.
The native AI chat surface is M2M and does not reuse SPA cookies or embed sessions.
Those are not part of the HappyM integration boundary.

Some providers require an external URL, a browser extension, Web3 signing or
manual action in their native app. The catalog reports those flags; SDK support
does not remove the provider's own onboarding or platform restrictions.
