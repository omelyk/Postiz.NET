# Stable release 3.1.0 for HappyM 3.0

This release changes packaging metadata only over qualified dev f3a982fe92b715af4cc4b35136f803dfb2fddc76 (producer 86f47166de6c9b7aaaccc1666283010a7a4abe5f). Preserve existing native qualification; do not replay local QA, Host, engine or editorial Note tests.

1. Merge the scoped stable metadata PR into dev only after ordinary CI passes on its exact candidate. Directory.Build.props and VERSION must both report 3.1.0.
2. Produce Postiz.NET, Postiz.NET.DependencyInjection and Postiz.NET.AspNetCore 3.1.0 plus their symbol packages through the existing build/test/pack workflow on net8.0/net9.0. Use a finite Root SDK slot for any local producer work. Do not rename or repack old prerelease ZIP contents as a stable package.
3. Record the producer SHA, package SHA256/SHA512, repository commit and dependency versions in the release cohort. DI/AspNetCore must depend on stable Postiz.NET; consumers use [3.1.0].
4. Root owns the feed publication. Publish generated packages to the approved GitHub Packages feed only after exact-candidate CI; the existing build workflow creates artifacts and does not publish them. Then qualify the private wrapper/UI cohort and update its exact pins together with the CRM owner.
5. Promote the admitted dev commit to main through the ordinary release PR. Create v3.1.0 at the admitted commit, publish the final changelog and attach checksums/SBOM evidence. Never move an existing tag.

The frozen appliance candidate remains upstream 2.23.0 plus its explicitly admitted HappyM source. Additional engine source requires its own explicit publication scope; this SDK metadata PR does not authorize it. Registry/image identity and cloud activation remain independent evidence. No new local runtime, real-provider publishing, upstream upgrade or HangFire change is required here.
