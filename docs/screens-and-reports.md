# Screens, Forms, Dashboard, and Reports

Status: Planned interface. No screens or generated documents have been implemented.

## Navigation and common behavior

Use a Windows navigation pane with Dashboard, Customers, Bookings, Calendar, Documents, Money, Purchases, Supplies and Assets, Owners and Debts, Reconciliation, Reports, and Settings/Backups. Group related records under these areas rather than exposing every database table as a menu item.

Always show active owner, TEST/LIVE mode, local save status, and backup status. Offline means cloud work is pending; it must not imply that a saved local record was lost. Use readable labels, visible keyboard focus, touch-friendly controls, inline validation, and keyboard navigation. Validate the final layout on the actual Surface at its normal display scaling.

Entry forms and editable draft grids use the same validation and save commands. Preserve unsaved work or ask before discarding it. Posted rows open details/correction actions rather than editable financial cells. Searching, filtering, archiving, and viewing linked history must not require SQL or code edits.

## Entry forms

Detailed field definitions and required-state rules are in the [data model](data-model.md).

| Form | Owner inputs | Result and validation |
|---|---|---|
| Customer | Name; optional contact/billing; preferences; sensitivities; notes; photo permission/evidence | Save with name only. Derived contact warning. History and balances read-only. |
| Booking inquiry | Primary contact if known; event/date/location; ready-by time; requested services/counts; artist; notes | Save incomplete inquiry with explicit unresolved fields. No automatic reservation. |
| Participants | Existing customer or new name; role; existing unnamed place | Fill a place without increasing quantity. Separate action changes headcount. |
| Appointment | Booking/place(s); trial/wedding; artist; date/start/end; location; products/shades; private notes | Show overlap and ready-by conflicts; record justified overrides. |
| Quote/invoice | Booking; payer; source charges; quantities/prices; discount; travel; tax decisions; dates; terms | Show live calculation, source coverage, draft issues, and PDF preview before issuance. |
| Payment plan | Trial amount; deposit rule/custom milestones; due dates; payer shares | Validate exact total equality and show residual cent. |
| Payment | Date; payer; booking/invoices; gross amount; method; actual account; reference; evidence; applications | Save draft, preview invoice/ledger effects, then approve once. Identify excess/unallocated value. |
| Expense/purchase | Merchant/supplier; purchase/service/payment dates; lines; category; business-use share; tax paid; account/owner funding; receipt | Capture unpaid bill separately from its payment. Flag missing evidence; prevent duplicate expense on later reimbursement. |
| Refund/credit/cancellation | Original booking/payment; request date; decision/reason; amounts; destination/new booking; evidence | Preview combined credit note, application reversal, cash, tax, and ledger effects. Require approval. |
| Supply | Item/brand/shade; purchase; opened/expiry dates; status; location | Update simple stock state, not a valuation or automatic expense. |
| Asset | Item/owner; dates; cost/evidence; funding; business use; approved book/tax values | Unknown fields remain unknown. Depreciation/disposal are separate reviewed entries. |
| Mileage | Date; driver; purpose/booking; actual route/miles; tolls/parking; reimbursement | Independent of customer travel price. |
| Owner transaction | Owner; contribution/draw/loan/reimbursement; date; account; amount; reason/evidence | Show effect on the appropriate owner balance, not an automatic percentage split. |
| Loan/payment | Lender/terms/evidence; opening/proceeds; principal/interest/fees; date/account | Components reconcile to cash. Reject excess principal repayment. |
| Transfer/payout | From/to accounts; date; gross/fee/net links; reference/evidence | No new income on transfer; fees separate. |
| Bank import/manual line | Account; statement period/balances; file/column mapping or line details | Preview rows, dates, signs, and duplicates before importing. |
| Reconciliation | Statement; matches; outstanding items; supported corrections | Show explained/unexplained totals and completion blockers. |
| Approval queue | Draft selection; evidence; category; proposed effects; accept/reject reason | Both owners may approve their own work. Revalidate stale drafts and record actor/time. |
| Tax setup/remittance | Treatment/evidence; jurisdiction/rate/dates; period; remittance/reference | Distinguish unresolved tax, liability paid, and return filed. |
| Business/backup setup | Private business details; accounts/openings; owner profiles; Google connection; destination; recovery settings | No real setup values preloaded into public examples. Test backup/restore separately from local save. |

File selection copies receipts/photos into managed local storage so later moving the original does not break the record. Display evidence privately and check permission context before using customer photos.

## Dashboard cards

| Card | Definition | Drill-down |
|---|---|---|
| Cash by account | Posted balance as of selected date; separate bank, physical cash, and processor clearing | Account movements plus last reconciliation date and opening-status warning |
| Income / expenses / result | Approved recognized entries within selected accounting period | Profit and loss with basis label |
| Customer amounts due | Open payer invoice balances as of date; separate available credits/unallocated receipts | Customer balance and aging |
| Upcoming weddings | Confirmed bookings in a separately labeled future date window | Event list, outstanding deposit/final milestones, artist |
| Pending work | Draft approvals, unresolved checks, missing due dates, tax setup | Review queue or affected record |
| Supplies | Low items and expired items as of business date | Supply record |
| Debt and tax | Principal and tax liability from approved entries; unknown setup clearly marked | Debt/tax schedule |
| Backup health | Last successful Sheets export and full archive, pending changes, failures, last restore test | Backup history/retry/restore |

Changing a date range must update its applicable cards and charts together. Show the period or as-of date on every card. Future booking value is a commitment, not earned or received income. Draft amounts never inflate posted totals.

## Charts and their datasets

| Chart | Dataset and calculation | Display/filter rule |
|---|---|---|
| Monthly income and expenses | Recognized income and expenses grouped by accounting month, including dated corrections/refunds | Clustered columns; selected accounting period; zero months shown only when records/opening context is known |
| Monthly result | Monthly recognized income minus recognized expenses | Line; same months/basis as income and expenses |
| Customer aging | Open milestone shares, or unscheduled invoice residual, counted once as of selected date | Horizontal bars: Not due, 1-7 days, 8-30 days, Over 30 days; Undecided due date separately |
| Upcoming confirmed weddings | Confirmed booking value after approved adjustments, plus count, grouped by event month | Columns for value; labeled count alongside; separate future window and explicit tax inclusion label |
| Expense categories | Recognized expense entries grouped by report category, net of relevant reversals | Horizontal bars; excludes transfers, draws, and loan principal; approved depreciation shown explicitly |
| Owner movements | Contributions, withdrawals, and loan movements by owner over period; outstanding loan shown as-of | Grouped columns; categories kept separate, never a single percentage-based balance |

For aging, due today belongs in Not due; tomorrow it is one day overdue. Sum aging bands plus undecided-date balances to the customer amount due. Do not count the same invoice both as a whole and again through its milestones.

For booking contribution, subtract assigned direct costs from attributable net charges excluding sales tax. Label this as an estimate of contribution, not exact net profit; simple supply status cannot establish actual per-client product cost.

## Financial and operational reports

| Report | Contents | Date basis |
|---|---|---|
| Profit and loss | Income, expenses, result, relevant book adjustments | Accounting date within period; cash-basis management label |
| Balance sheet | Assets, liabilities, owner equity/current result; equality check | Cumulative through as-of date; unconfirmed opening warning |
| Trial balance | Account opening, period debits/credits, ending debit/credit balance | Period plus cumulative opening; debit/credit equality |
| Cash movements | Opening, receipts, payments, transfers, financing/owner movements, closing by account | Actual transaction dates and posted cash ledger |
| Customer balances | Payer invoices, applications, credit notes, unused credits, unallocated receipts | Historical as-of date, not today's mutable totals |
| Supplier balances | Purchases, payments, supplier credits/refunds, unpaid amounts | Operational purchase/payment dates |
| Owner schedule | Contributions, withdrawals, loans, reimbursements per owner | Period and as-of totals; not tax capital/basis certification |
| Asset schedule | Cost, additions, approved depreciation, carrying value, disposal | As-of and movement period |
| Debt schedule | Opening principal, proceeds, principal paid, interest/fees, closing, due dates | Transaction dates; principal checked after every movement |
| Tax summary | Charged, collected, refunded, liable, remitted, remaining, filing status | Confirmed tax timing and selected tax period |
| Booking report | Services/places, appointments, payer shares, amounts received/due, direct costs/contribution | Booking/event and separately labeled cash dates |
| Mileage/supplies | Actual trips/reimbursements; Low/Finished/expiry states | Trip period or inventory-status as-of where history exists |

Every export includes business display name, TEST/LIVE, basis, filters, generation time, and committed-data watermark. Preserve historical report snapshots for closing; don't replace them when definitions change.

## Client-ready documents

Use a clean branded layout derived from the business's existing quote structure. Recreate it with fictional data in the repository; do not copy a real customer's quote or contact details into examples.

| Area | Content |
|---|---|
| Header | Business name/logo and configured public contact details; document type, number, version, date |
| Client and event | Intended recipient/payer, relevant billing/event details, venue and appointment summary |
| Lines | Service descriptions, quantities, unit price, discount, wedding travel and units |
| Totals | Net charges, resolved tax, total; credits/applications and balance with an as-of date |
| Payment plan | Trial/deposit/final or custom milestones, assigned payer amounts, actual due dates |
| Terms | The version issued for this booking, acceptance information where appropriate |
| Footer | Page numbering, document reference, TEST watermark when applicable |

A receipt shows its posted payment, date, method, reference where appropriate, applications, and balance as of issuance. Later refunds do not rewrite the original receipt. Credit notes identify the invoice/lines adjusted. Issued PDF content is stored and hashed; reprinting uses that version.

Use an explicit allowlist of client-facing fields. Exclude sensitivities, private notes/photos, internal costs, owner balances, credentials, and unrelated clients. Preview multipage output, long names, fractional miles, split payers, and odd-cent totals before release.
