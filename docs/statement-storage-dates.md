# Preserve bank-statement dates across calendars

Bank statement periods and bank-line dates use Gregorian ISO storage independently of the Windows calendar. Imported JSON source rows retain their original dates. Statement lists, bank lines, retained source rows and exact import retries use the shared storage-date parser.

Four probes failed before this change. Imports under `ar-SA` and `th-TH` stored January 1, 2027 as `1448-07-23` and `2570-01-01`. Reading a previously imported Gregorian statement threw under Arabic settings or returned different dates under Thai settings.

After changing two production files, all 52 related Release cases pass: statement dates, CSV parsing, import/withdrawal, no-activity statements and bank matching. Windows Debug builds with zero warnings and errors. The new probes also check that exact retries leave the watermark/audit unchanged and import creates no financial movement.

This change does not rewrite earlier stored data. Reconciliation locks, completion/history dates, match-write dates and carried-error queries are separate boundaries under review. Complete release and actual-device acceptance remain pending.
