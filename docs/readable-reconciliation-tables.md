# Project completed reconciliation history

Thirteen source-bound tables retain approvals and reopen state, frozen balances, bank/cash positions, outstanding cash identities, available posted corrections with explicit selection, prior reconciliation dependencies, observed/remaining bank errors, error claims, recorded warnings, bank coverage, original errors, resolutions, and reopen provenance.

`ObservedWatermark`, `GeneratedAtUtc`, balances, positions, and readiness describe the saved reconciliation. They do not describe current bank balances or newly recalculated readiness. A reopened completion stays in history; reopening does not erase its reviewed source or hash. `Remaining` error rows retain confirmed zero amounts. Bank corrections and matching move no additional journal money.

The captured database verifies retained snapshot hashes. Projection also requires the saved statement/account/date identities to match relational facts. It emits selected scalar fields and links rather than raw snapshots. Free-form reconciliation/outstanding/error/reopen explanations and evidence inputs stay in the desktop and encrypted archive. Public bank and posted-correction descriptions remain readable.

Three fictional checks pass for reopened frozen balances after later cash changes, an actual bank error and correction with no cash postings, and a changed snapshot identity even when its content hash has been updated. All tables share the captured export generation/watermark and budget. This remains a partial projection; Sheets publication follows separately.
