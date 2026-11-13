# Verify retained client draft discard history

Protected history reads now validate canonical hashed events and their original typed quote/invoice/receipt/credit-note snapshots. Indexed identity, request, payload, kind, revision, mode and recording owner must agree. The current draft must be the exact one-revision discarded result, have no issued version, and retain the original fields. Discarded records without retained facts are rejected. Historical actors may be inactive; current access still requires a valid owner session.

Backup capture uses the same validation for schema 40. A rehashed wrong-mode event initially passed archive capture; after integration, that probe rejects it. Recomputed outer hashes cannot hide a changed mode, original draft, current status or missing retained fact.

Fifty-two unique related cases pass across the main history/rules/archive/upgrade run and added recovery/privacy checks. They cover all four kinds, corruption, source/index mismatches, late issued linkage, inactive historical actors, access/cancellation and unchanged issued documents/money. An actual encrypted temporary recovery retains all four private histories with fresh instance/owner access. Complete readable generation keeps 165 source tables and excludes the private reasons and raw retained snapshots.

Windows Debug and native TEST profile reopen/lock/close pass, preserving the schema-40/watermark-5 fixture, three private profile revisions and managed file. The full schema-40 parent regression passes 1,544 cases in 7 minutes 6 seconds at `0996ee2`. Transactional discard commands and native review follow separately; no public discard write operation is added here. Release and actual device/account acceptance remain pending.
