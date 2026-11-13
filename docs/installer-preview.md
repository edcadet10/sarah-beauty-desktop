# Build a private installer preview

The preview installs the bundled Windows x64 app for the current Windows account. It creates a Start menu shortcut and an Apps uninstall entry. It copies explicit package files and removes only those files and empty package folders. Business databases and encrypted archives are excluded.

Publish the app first, then run:

```powershell
pwsh -NoProfile -File tools/build-installer.ps1 -NsisCommand C:/Tools/NSIS/makensis.exe
```

Use NSIS 3.13 from its [official download page](https://nsis.sourceforge.io/Download). The command checks the compiler version and fails on compiler warnings. The unsigned preview appears in `.artifacts/installer`. NSIS dependency notices are included.

The normal destination is the current account's `LocalAppData/Programs/SarahBeautyDesktopPreview` folder. A different `/D` destination is rejected. Private QA builds can use `-ReviewSuffix -QAexample` and `-ReviewInstallDirectory` inside this checkout's `.artifacts/installer-review` folder. Do not distribute QA builds.

## Checks on November 12, 2026

The private fixture used fictional TEST records and a destination containing spaces. The initial 1.0.0.0 package and different-version 1.0.1.0 update each matched all 574 published file hashes. The update preserved every business-file byte and unrelated top-level, nested, and database-named sentinels. The updated executable read retained customer, cancellation, PDF, and reopened reconciliation records, refreshed expected balances, and reopened under the second owner. One combined automation attempt lost its window reference; a standalone native rerun passed.

Uninstall removed all packaged files, its uninstaller, shortcut, and per-user registration. All business-file bytes and unrelated sentinels remained unchanged. These are checks on this development machine, not clean-machine or Surface acceptance.

Run the small native destination regression probe with:

```powershell
pwsh -NoProfile -File tools/test-installer-paths.ps1 -NsisCommand C:/Tools/NSIS/makensis.exe
pwsh -NoProfile -File tools/test-installer-package.ps1
```

The native probe passes for a nonexistent destination containing spaces, rejects another destination, and rejects a junction ancestor without creating app files. Package checks continue to pass. Install and uninstall also check directory ancestors for reparse points and require the app to be closed.

## Corrected path failure

The first private install exposed a defect: NSIS `GetFullPathName` returned an empty string for a nonexistent destination. The old comparison accepted two empty paths, and extraction created 374 files at the drive root before failing. No business files changed. Each unexpected file's creation event, file ID, and payload hash was verified before moving it into reversible workspace quarantine. All 374 verified files were removed from the drive root without changing their bytes; quarantine and evidence are retained privately.

The guard now calls Windows `GetFullPathNameW`, rejects empty, failed, oversized, or different results, and preserves the validated destination. The native regression probe checks the original nonexistent-folder trigger.

## Remaining release work

No installer has been released. The preview is unsigned. Interrupted copy/update recovery, broader Windows and Surface checks, clean-machine installation, signing, distribution terms, and update policy remain pending. The preview does not claim transactional rollback of a partially copied app. Business data remains outside the installation folder.

References: [NSIS instructions](https://nsis.sourceforge.io/Docs/Chapter4.html), [NSIS license](https://nsis.sourceforge.io/License).
