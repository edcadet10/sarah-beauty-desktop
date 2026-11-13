# Initialize database modes explicitly

The internal initializer now requires an explicit expected mode. Fresh initialization records TEST or LIVE atomically with the store identity and schema. An existing mode must match; opening never converts TEST to LIVE or silently assigns LIVE to a legacy file with no mode. Valid version-0 TEST metadata retains its earlier upgrade path.

An existing-only open does not create a missing file or initialize an empty file. Upgrades retain mode/identity and a verified openable snapshot. Competing initializations with different modes cannot both win the same empty file. Empty initialization creates no business, owners, profile reviews, money or LIVE setup authority.

Thirty-eight mode/legacy-initialization/backup cases pass. The compatibility run initially found five message-text assertion failures; the operation message now retains the existing required-mode phrase. One added legacy fixture had an unsupported shape; it now uses the faithful version-0 metadata fixture and checks the actual LIVE-mode rejection before a valid TEST upgrade. Windows Debug has zero warnings/errors. Native TEST profile reopen/lock/close and read-only fixture checks pass unchanged.

LIVE-mode databases in these tests are empty temporary fixtures. Public business-store opening and the application still support TEST only. Mode-aware store/document access, clean business creation, native selection and release acceptance follow separately.
