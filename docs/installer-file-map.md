# Generate exact installer file lists

The installer package module maps published files to explicit NSIS copy and removal instructions. Removal names individual packaged files and then attempts to remove empty folders, including parent folders. It emits no wildcard deletion or recursive directory removal.

The module rejects linked source/output directories, unsupported installer macro syntax, database/SQLite sidecar extensions, encrypted recovery archives and renamed SQLite/age files identified by their headers. Output ancestors are checked before creating a new output directory. Dollar signs remain literal in filenames.

Run the adversarial checks with:

```powershell
pwsh -NoProfile -File tools/test-installer-package.ps1
```

Checks pass for literal filenames/parent folders, six excluded extensions, renamed SQLite/age headers, macro rejection, linked sources and linked outputs without creating a directory through the rejected junction. The complete bundled publish maps all 574 files. An intermediate multiline condition caused a parser error; it was corrected before the final passing run.

This is a packaging component. It has not installed, updated or removed an application. Runtime destination guards and actual preservation of unrelated/business files remain part of the separate installer rehearsal. Private fixtures and generated includes remain outside Git. NSIS describes the relevant commands in its [scripting reference](https://nsis.sourceforge.io/Docs/Chapter4.html).
