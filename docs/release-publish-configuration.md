# Preserve serialization in published Windows builds

The desktop project now disables trimming for every configuration. Its persistence, view-model, PDF and Google code uses reflection metadata that must remain available in a published build.

The previous default Release publish completed with trimming warnings and generated a runtime configuration that disabled JSON reflection. Launching that exact published executable against the fictional TEST fixture reproduced the disabled-serialization error after sign-in. Debug builds and the storage test host did not reproduce that deployment configuration.

After the project change, the default `win-x64` Release publish completes and its native executable passes customer read/save, retained cancellation/PDF/reconciliation reads, refreshed financial/customer totals and second-owner restart. An independent check preserves the original cancellation/issued/banking history and all four PDFs. The new private customer note remains after restart without appearing in old issued documents. Windows Debug builds with zero warnings and errors. The November 12, 2026 NuGet audit reports no known vulnerabilities in the resolved desktop dependency graph; this is a scoped audit result, not a security certification.

To repeat the publish after building the pinned recovery helper:

```powershell
dotnet publish src/SarahBeauty.Desktop/SarahBeauty.Desktop.csproj -c Release -r win-x64 -o .artifacts/publish/release-win-x64
```

The output contains the app, recovery helper and included notices. This default untrimmed output still depends on installed .NET/Windows App SDK runtimes. Bundled-runtime publishing, installer/update/uninstall checks, signing, actual Surface operation and second-computer recovery remain required before release. No installer or release artifact was published.

Microsoft documents [the JSON behavior of trimmed applications](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/8.0/publishtrimmed) and [the separate .NET and Windows App SDK self-contained settings](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps). Enabling JSON reflection alone would not preserve every reflected type removed by trimming; this change keeps the metadata used by the current implementation.
