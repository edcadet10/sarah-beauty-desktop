# Project current and retained ledger reports

Six source-bound tables retain report/date/mode/watermark/time scope, trial balance, profit and loss, balance sheet, cash activity, and ledger opening warnings. Current reports use the shared exact ledger calculation against the captured database. Retained period reports use their original saved values, dates, account facts, and observation watermark/time, including after reopening.

Every table belongs to the current export generation/watermark. Each report's `ObservedWatermark` identifies the source of its figures. `CapturedSource` and `RetainedPeriod` rows are separate reports; do not add them together. Current warning rows cover cash-account openings; full domain warnings and non-ledger schedules follow separately. Account active flags describe facts at that report's capture; opening confirmation follows its as-of date.

Cash activity repeats existing journal sides for review. Loans, contributions, withdrawals, and tax are not treated as income or expenses merely because cash moves. Public movement descriptions remain readable; no raw snapshot or close/reopen evidence input is emitted. Stable current report times come from the captured source, so repeated projection does not change hashes.

Two fictional cases pass for exact income/fee/expense/tax/loan/owner/cash distinctions with later-write isolation and locked human access, and reopened retained reports alongside current reports after a later expense. Their trial balances match the existing financial report. The preceding complete Release suite passes all 903 cases; Windows Debug has zero warnings/errors. Additional reports and verified Sheets publication remain pending.
