# Publish the Windows desktop app with bundled runtimes

Use a Windows development computer with .NET 10, the Windows build tools and Go 1.27.4. Restore uses the versions pinned in the projects. To build the recovery helper and publish Windows x64:

```powershell
pwsh -NoProfile -File tools/publish-desktop.ps1
```

If Go is not on `PATH`, pass its executable with `-GoCommand`. The command publishes to `.artifacts/publish/win-x64` and reports success after checking the app, .NET runtime, WinUI runtime, recovery helper and required notices. Keep the complete output directory together.

The `win-x64` profile bundles both .NET and Windows App SDK, disables trimming and keeps native runtime files separate. Windows App SDK license/notice files accompany the existing dependency notices. The script builds recovery before project evaluation so its conditional content is included.

Publishing also copies [runtime package license and notice files](runtime-package-notices.md) from the resolved dependencies, checks their hashes, and records every runtime NuGet package in `ThirdParty/RuntimePackages/inventory.json`. An unreviewed package/version or missing required source fails the command.

On November 12, 2026, the output contains 574 files totalling 314,056,984 bytes, including .NET 10.0.15, WinUI, the matching recovery helper and all nine image/icon assets. The SDK notices and helper match their source hashes. The directory contains no business database or encrypted recovery archive. The native published app passes customer/cancellation/PDF/reconciliation reads, refreshed balances and second-owner restart on the development computer; the previous untrimmed publish separately passed a persisted customer save. The complete parent storage run passes 1,681 cases at `39225e7`.

These checks do not establish clean-computer installation, actual Surface touch/offline behavior, update/uninstall data preservation, signing, real Google delivery or second-computer recovery. This command creates a local review artifact; it does not install or publish a release.

Microsoft explains the separate runtime settings in [Deploy Windows App SDK apps as self-contained](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps). See [Preserve serialization in published Windows builds](release-publish-configuration.md) for the reproduced trimming failure and fix.
