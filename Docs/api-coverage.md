# API coverage

Supported against Postiz `v2.23.0` `/public/v1`:

| Area | Routes |
|---|---|
| Health | `GET is-connected` |
| Groups | `GET groups` |
| Integrations | list, settings/schema, provider tool trigger |
| Media | multipart upload, upload from public URL |
| Posts | list, create draft/now/schedule/update, find slot, missing content, status, delete post/group |
| Analytics | integration and post analytics |

Not exposed by the pinned Postiz Public API and therefore intentionally absent
from the alpha SDK:

- webhook CRUD/signature contract;
- a product version/compatibility endpoint;
- update of integration settings;
- publication lookup by an external registry ID.

These are fork API gaps, not SDK placeholders. They must be added and contract
tested in HappyM.Postiz before public SDK methods are introduced.
