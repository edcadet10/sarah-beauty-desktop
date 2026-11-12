# Read captured report dates across Windows calendars

Stored business dates use Gregorian `yyyy-MM-dd` text. Captured financial reports, dashboard queries, and retained-report identity checks now format query dates and parse stored dates with an explicit invariant culture. The shared `StoredDates` helper keeps those operations small and consistent. Human date-entry rules remain separate.

## Reproduce the failure

The fictional fixture captures real issued invoices, applications, receipts, loan proceeds, expenses, and asset movements. It reads the same source under the normal test culture and then under Arabic (`ar-SA`) and Thai (`th-TH`) cultures.

Before the fix, Arabic settings caused `DateOnly.Parse` to reject `2027-01-10`. Thai settings formatted query years using the Buddhist calendar, which returned empty cash activity. A second fixture exposed a remaining ledger-table parse that changed `2027-01-10` into Gregorian `1484-01-10` under Thai settings. All failures were observed before their corresponding fixes.

The six calendar cases compare complete serialized financial reports, closed-period history, reconciliation tables, ledger report tables, and both dashboard views from the same capture. The upcoming-event fixture includes an approved deposit and confirmed booking. Tests restore the prior culture in `finally` and do not change the process-wide default culture.

## Scope

The fix covers the captured report paths and shared readers they use. Shared document-version, credit-funding, and supplier-settlement writes in those files also use the invariant format. It does not claim that every remaining entry, validation, or storage path has been converted. Those paths require separate mutation checks. Existing incorrectly dated records are not guessed, rewritten, or migrated.

Microsoft documents the explicit format and culture provider accepted by [DateOnly.ParseExact](https://learn.microsoft.com/en-us/dotnet/api/system.dateonly.parseexact?view=net-10.0). Documentation follows the [Google developer documentation style guide](https://developers.google.com/style/highlights).
