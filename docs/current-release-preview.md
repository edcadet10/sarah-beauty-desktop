# Verify the current bundled preview

Date: November 12, 2026. Application source: `cbe4e7a`, schema 40. Installer source: `af61739` from the separate installer-preview change. Environment: developer Windows machine, fictional TEST records. This checks the current emerald UI in a bundled executable and installed app; it is not a public release.

## Publish and native checks

`tools/publish-desktop.ps1` builds the recovery helper before publishing the untrimmed self-contained Windows x64 profile. The current output contains 574 files, 314,132,248 bytes, and bundled .NET 10.0.15, WinUI, recovery, assets, and notices. File version remains 1.0.0.0.

The bundled executable reads grouped private customer details, retained cancellation and its issued credit-note PDF, and reopened reconciliation. It refreshes the expected $70.00 income, $54.07 expenses, $15.93 Bank, and $40.00 funded-credit balance. Numbered chart legends render. A second-owner restart reopens the retained customer details. Independent checks retain original financial/cancellation history and all issued PDF hashes.

One automation attempt lost its window reference. No crash event for this preview was observed in the scoped check; the standalone native rerun passed. That failed automation attempt is not counted as a successful run.

## Current installer check

The existing guarded installer compiler packaged this published output into an unsigned private QA installer. It installed into a new designated path containing spaces, created the per-user shortcut and correctly quoted uninstall registration, and matched all 574 published file hashes plus the included installer notice.

The installed executable passed the same retained-customer, cancellation/PDF, reconciliation, exact-balance, chart, and second-owner restart checks. Uninstall then removed the packaged files, uninstaller, shortcut, and per-user registration. All business-file bytes matched the baseline taken before each installer operation. Unrelated top-level, nested, and database-named sentinels remained unchanged.

Private fixtures, installers, runtime bundles, customer records, and detailed QA artifacts remain outside Git. The application and installer source are saved in their focused GitHub changes.

## Release limits

No installer has been publicly released or signed. This is one development-computer integration check. Interrupted updates, clean-machine and actual Surface behavior, physical touch/keyboard/offline checks, second-device recovery, real owned Google authorization/private delivery, and the remaining acceptance walkthrough continue separately. Do not treat these checks as LIVE readiness.
