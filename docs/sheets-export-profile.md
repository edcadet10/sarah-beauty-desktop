# Save report dates and an automatic export choice

Owners can save the financial date range, planning date, event range, automatic-export choice, and minimum interval of 1 to 10,080 minutes. Dates remain fixed until an owner changes them; this profile does not silently roll report windows forward. A new or recovered writable instance starts without a confirmed profile.

The revision check prevents stale forms from replacing newer settings. Saving an unchanged draft creates no extra revision or audit entry. Changes are audited once. Cancellation, owner access, and active instance are checked inside the writer transaction. An automatic schedule requires its current saved profile and an active confirming owner, without renewing their session.

Report settings are separate from Google output consent. Saving or withdrawing automatic settings needs no internet connection and does not enable Sheets delivery. The future queue also requires current Google Sheets permission. No profile exists by default, so no automatic export can start from this change alone.

The focused tests cover actual reopen, fixed selection persistence, no-op saves, owner/foreign/stale access, interval/date bounds, writer cancellation/instance change, offline withdrawal, inactive confirming owner, and restored-instance isolation. Three initial assertions incorrectly assumed the fixture started at watermark zero; they now compare the recorded initial watermark, with no production behavior change. Scheduling execution and controls remain subsequent work.
