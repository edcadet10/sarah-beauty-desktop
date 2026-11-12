# Project imported statements and matching history

Seven source-bound tables retain statement balances and approval hashes, explicit CSV mapping fields, original imported row facts, bank transaction identities, statement withdrawals, match approvals, and dated match portions/releases. Empty source mappings stay unknown. A confirmed zero-activity statement retains its zero balances without invented transactions.

Withdrawn sources remain visible. `HasActiveStatement` identifies bank lines referenced by at least one import that has not been withdrawn. A corrected reimport can have a new bank-line identity and the same content hash; do not deduplicate the exported history by that hash. Original signed cents reflect the reviewed import mapping. Match releases name the original match; positive portions and releases are separate history, not additional cash.

Free-form source-choice, withdrawal, match-review, and evidence text stay in the desktop and encrypted archive. No raw JSON or binary source is exported. Formula-like descriptions remain literal strings for the later RAW Sheets writer. All rows share the captured generation and watermark and consume the shared text/cell budget. This is a partial projection; no workbook is published.

Three fictional checks pass for an actual withdrawn/corrected import with opposite signs and distinct identities, an actual partial match/release with one posted receipt, and a confirmed no-activity statement with unknown mapping. Reconciliation projections and verified Sheets publication follow separately.
