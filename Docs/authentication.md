# Authentication

Postiz v2.23.0 accepts either the organization API key or an OAuth token that
starts with `pos_` in the raw `Authorization` header. `Postiz.NET` accepts the
credential through `PostizOptions.ApiKey` and applies it per request.

Credentials are never included in exceptions, response bodies or log messages.
Use a secret provider in production and a dedicated Postiz organization/key per
environment. The SDK rejects non-HTTPS remote base addresses; loopback HTTP is
allowed for local development.
