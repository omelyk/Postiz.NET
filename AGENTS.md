# Postiz.NET

## Ownership and required context

This independent SDK owns typed Postiz Public API transport, DTOs, domain clients,
DI registration and ASP.NET Core health checks. It owns no CRM rules, tenant
mapping or database access. Read `README.md`, `Docs/istruzioni.md`,
`Docs/authentication.md`, `Docs/errors-retries.md`, `Docs/compatibility.md` and
`Docs/release.md`. The scaffold in `Docs/prompt_zero.txt` is historical.

Use `Directory.Build.props` for the actual package version and target frameworks;
older alpha references in narrative documents are not the current build version.
Do not infer compatibility with an unpinned or latest Postiz image.

For HappyM ecosystem work explicitly read the verified coordination workspace's
`AGENTS.md` and `_codex-project/PROJECT-MAP.md` plus scope documents. The canonical
checkout is under its `packages/` directory (`../../`); independent Git roots
must not assume automatic parent instruction discovery. Standalone clones remain
independent and consume no implicit sibling checkout.

## Contract and safety boundaries

- API credentials use the owner-defined Authorization contract; never log or
  version them. Preserve organization scope and write-only provider secrets.
- Preserve the documented retry boundary; do not retry uncertain writes merely
  because transport failed. Consumers own reconciliation of those outcomes.
- Do not modify the external Postiz fork or Pharma integration implicitly.
- Local appliance lifecycle belongs to HappyM.Platform.Local, not this SDK.
- Integration tests are opt-in and can contact a real instance. Do not set or
  reuse `POSTIZ_TEST_URL` / `POSTIZ_TEST_API_KEY` without an explicit test target.

## Local verification and artifacts

Run from the repository root. Safe unit/fixture checks excluding live integration:

```powershell
dotnet restore Postiz.NET.slnx --configfile NuGet.config --disable-parallel
dotnet test Postiz.NET.slnx -c Release --no-restore -m:1 --filter "FullyQualifiedName!~Postiz.NET.IntegrationTests"
dotnet pack Postiz.NET.slnx -c Release --no-restore -o artifacts/packages
```

Both net8.0 and net9.0 are required. NuGet and symbol packages belong in ignored
`artifacts/packages/`. Live contract tests against a pinned instance are a
separate release gate; filtered tests do not prove that gate passed. For
documentation-only changes validate paths and the diff, not a fictitious runtime.

## Delivery

Use a scoped PR to `dev`; `main` is a separate release promotion. Preserve branch,
worktree and stash state. Do not change remotes, protection rules or Codex storage.
No real tags, package publication or deployment without release authorization.
Update compatibility and changelog for contract/release changes without silently
upgrading dependencies or versions during organization work.
