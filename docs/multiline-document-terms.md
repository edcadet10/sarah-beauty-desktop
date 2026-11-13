# Preserve multiline terms in document editors

Date: November 12, 2026. Fix: `cdecbc8`; bundled preview source: `9b667a8` (same application code). Schema remains 40.

Preparing an invoice from a quote with 18 paragraphs reduced its terms from 4,128 characters to the first 226-character paragraph. Preparing a revised quote reproduced the same loss. Both native read-only probes failed before the fix, with the app process still running. The issued test invoices' stored snapshots and PDFs confirmed the shortened terms; this was not only a display issue.

The editors created a single-line text box with its saved text, then enabled multiline input afterward. [Microsoft documents](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.textbox.acceptsreturn?view=windows-app-sdk-2.0) that `AcceptsReturn` defaults to false. A small shared `Form.MultilineInput` now enables returns and wrapping before assigning saved text. Quote and invoice terms use it. The control has a 100-pixel minimum and 220-pixel maximum height; other single-line fields keep their existing behavior. This follows the [text-box guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/text-box) for long editable text.

## Native regression and document checks

- The same two probes now retain all 4,128 characters. Quote and invoice drafts each survive two save/reopen cycles with exact terms, including the final paragraph and ending marker. Read-only checks confirm the saved draft revisions and equality with the issued quote.
- The checks leave original issued rows and PDFs unchanged. The owner then explicitly voids the two unused shortened test invoices with retained reasons, and issues corrected I-000003/I-000004. Original snapshots, hashes, PDFs, and all earlier coverage remain retained; none are rewritten.
- Corrected invoices retain the quote's exact 4,128-character terms, with totals $376.60/$1,100.00, exactly $1,476.60 combined. Active coverage accounts for all 23 charge lines once. Combined milestones remain Trial $150.00, Deposit $663.30, Final $663.30.
- The quote has four pages, corrected bridal invoice two, and corrected grouped-guest invoice four. Payer names are 148/146 characters, and the booking includes nine grouped guests and 20 long wrapped extra descriptions. First/middle/last quote pages, a guest continuation, both corrected bridal pages, and corrected guest terms were inspected. Table headings repeat; every source description, complete terms, address, and total is extracted from the matching PDF. Geometry checks find no words outside page bounds or body text overlapping headers/footers. Repeated headers/footers are excluded when comparing terms across page breaks.
- All PDFs retain TEST/page marks and their recorded hashes. Seeded private makeup, sensitivities, customer/booking notes, permission scope/evidence, and acceptance evidence, plus owner names, are absent from extracted text. Zero movements/journal lines are created; integrity and foreign keys pass.
- A second-owner restart retains both voided originals and corrected invoices/PDFs, with exact stored rows matching the final checkpoint. Intermittent stale UI Automation roots interrupted earlier attempts while the process remained alive; the read-only harness reacquires the app window. Interrupted attempts are not counted as passes.

## Build and scope

The recovery helper is built before project evaluation. Debug builds with zero warnings/errors. The earlier four PDF renderer tests pass; native before/after tests cover the WinUI initialization behavior those storage tests cannot exercise.

The fix also publishes as an untrimmed self-contained Windows x64 preview with bundled .NET 10.0.15, Windows App SDK, recovery helper, and notices: 574 files, 314,132,448 bytes. Native published-app quote/invoice preparation retains all terms, and second-owner restart opens all retained PDFs without changing the final facts.

This covers the long-name/grouped-guest/split-payer multipage quote/invoice scenario from AC-019 and fixes terms loss. It does not certify every PDF kind, all private-data categories, full accessibility, device behavior, or complete release acceptance. Physical devices, real Google authorization/delivery, signing, and other acceptance work remain open. No real agreement, payment, or tax policy is inferred from this fictional fixture.
