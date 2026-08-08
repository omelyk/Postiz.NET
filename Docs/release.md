# Release

1. Run restore, tests and pack in Release on .NET 8 and 9.
2. Run integration tests with `POSTIZ_TEST_URL` and `POSTIZ_TEST_API_KEY` against
   the pinned Postiz instance.
3. Verify API coverage and update the compatibility matrix/changelog.
4. Produce SBOM and dependency/secret scan evidence.
5. Tag prereleases as `v1.0.0-alpha.N`; publish the generated `.nupkg` and
   `.snupkg` to GitHub Packages only from an approved release workflow.

The build workflow deliberately creates artifacts but does not publish them.
