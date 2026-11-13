# Keep posting dates independent of the current calendar

The posting writer now uses the shared Gregorian storage-date formatter. Closed-period checks use the matching invariant parser, and loan-opening eligibility queries use the same storage formatter. Typed business dates, owner confirmation and posting transaction boundaries remain unchanged. This change does not infer or rewrite dates in historical records.

Four probes failed before the fix under `ar-SA` and `th-TH`. A January 10, 2027 expense was indexed as `1448-08-02` or `2570-01-10`. A real retained January closing then failed to block approval under the Thai calendar; under the Arabic calendar its stored boundary raised a parsing error. The approval probe was run against the unchanged implementation before applying the fix.

Fifty-six related cases now pass: both calendars preserve newly approved ISO dates across reopen, reject preview and approval in a closed period without changing its watermark/movements, and recognize an existing evidenced loan opening. Posting, openings, retained book periods and captured-calendar report cases also pass. Windows Debug passes without warnings or errors.

Other storage-date boundaries are reviewed separately. The preceding balance-guard full regression is running; the earlier discard-command parent passes 1,575 cases. Full workflow/release and actual device/account acceptance remain pending.
