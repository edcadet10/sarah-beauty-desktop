# Acceptance Criteria

Status: Required behavior for the future application. All application scenarios below are pending implementation and execution. Documentation review and arithmetic checks do not mark these scenarios passed.

## Initial agreed scenarios

### AC-001: Approve a valid expense

Given a valid draft expense, when either owner approves it, then the application records the expense once and saves the approver and approval time.

### AC-002: Approve your own entry

Given an owner has created a valid draft financial entry, when that same owner approves it, then the application records the entry once and identifies that owner as both its creator and approver.

### AC-003: Approve while offline

Given the Surface has no internet connection and a valid draft expense is ready for approval, when either owner approves it, then the application saves the expense and approval details locally, updates the financial totals, and marks the cloud backup as pending.

### AC-004: Save a name-only customer

Given an owner enters a customer name without a phone number or email address, when they save the customer, then the application creates a customer ID, saves the record, and displays "Contact information missing".

### AC-005: Add missing contact information

Given a customer has no phone number or email address, when an owner adds a valid phone number or email and saves, then the application saves the change and clears the warning.

### AC-006: Reject an empty customer name

Given a new customer name is empty or contains only spaces, when an owner attempts to save, then the application explains that a name is required and does not create the customer record.

### AC-007: Record unnamed bridesmaids

Given an owner creates a booking with six bridesmaids whose names are unknown, when they save, then it records six unnamed places without creating customer records for them.

### AC-008: Name existing participants

Given six unnamed bridesmaid places, when an owner links customers to two places, then the booking shows two named and four unnamed places, with quantity and price unchanged.

### AC-009: Separate recipient and payer

Given a bridesmaid receives a service that the bride will pay for, when an owner prepares the invoice, then the bride is its payer and the bridesmaid remains the service recipient.

## Remaining acceptance scenarios

Each row is a scenario: establish the condition, perform the action, and check the observable result. Use fictional records absent from the development fixtures as well as the example amounts.

| ID | Condition and action | Required result |
|---|---|---|
| AC-010 | Save two customers with the same name; remove both contact fields from one | Separate permanent IDs/history; warning restored only for the contactless record |
| AC-011 | Archive a customer referenced by an invoice | Customer disappears from default active lists, but invoice/history and balances remain intact |
| AC-012 | Quote $300 + 9 x $120 + 76.6 x $1, explicitly excluding tax for the fixture | $1,456.60 total; $150 trial, $653.30 deposit, $653.30 final; trial charged once |
| AC-013 | Split $1,456.61 with $150 trial, then use the custom-price fixture in business rules | $653.31/$653.30 and $301.46/$301.45 splits respectively; exact sum and defined rounding |
| AC-014 | Pay the trial only, then approve the required booking deposit | Trial alone does not reserve; deposit permits a recorded confirmation after other event requirements pass |
| AC-015 | Create single-payer and split-payer versions of the same booking | Same booking total; every issued share accounted once; unresolved travel payer blocks affected issuance |
| AC-016 | Convert the same accepted quote charge twice, including simultaneous/retried requests | No overbilling; explicit remaining/unbilled amount on partial conversion; unique source coverage |
| AC-017 | Edit catalog price, customer billing address, and current terms after document issuance | Previously issued version/PDF unchanged; new draft can use new values |
| AC-018 | Issue a live document with unknown tax; repeat using an explicitly resolved fictional test rate | Live unknown is blocked; TEST fixture has exact tax/total/milestone equality and visible TEST label |
| AC-019 | Render long names, grouped guests, split payers, and multipage documents with seeded private notes | Readable layout and correct totals; private notes, sensitivities, owner data, and credentials absent |
| AC-020 | Record a $120 receipt, $3.60 fee, and $116.40 payout | Customer credit $120; income $120 in no-tax fixture; expense $3.60; bank $116.40; clearing zero; no second sale |
| AC-021 | Approve one draft repeatedly, then enter its same reliable external payment reference through another entry screen | Exactly one real receipt; retry returns existing result; conflicting duplicate is flagged |
| AC-022 | Enter two legitimate same-date/same-amount cash receipts with distinct evidence | Reviewable similarity warning without automatically deleting/rejecting the second real payment |
| AC-023 | Inject failure after each stage of financial writes; reopen and retry | All-or-nothing database state; one final batch; no duplicate cash, applications, audit approval, or journal lines |
| AC-024 | Approve a stale edited draft, invalid date, negative amount, missing account, or amount beyond supported range | Clear rejection before financial mutation; unrelated valid drafts remain usable |
| AC-025 | Apply a payment across invoices; apply many receipts to one invoice; exceed either side by one cent | Valid applications reconcile; excess fails; milestones never subtract twice |
| AC-026 | Submit two $200 credit uses against $326.65 available | At most one succeeds; remaining credit $126.65; no negative balance after retry/restart |
| AC-027 | Refund an applied payment with and without cancellation of the charge | Application reverses once; valid charge reopens; cancelled charge has matching adjustment; cash/tax/customer balances agree |
| AC-028 | Approve a case-specific retained/refunded/transferred cancellation, then reuse its available credit or refund it | Source and decision retained; bounded amounts; credit funding cannot also be spent as unused payment; refund releases funding and consumes cash once; no duplicate receipt/income |
| AC-029 | Run the contribution/loan/service/expense/asset/principal/draw fixture | Cash $1,170; asset $300; debt $400; equity including result $1,070; result $120; trial balance and balance sheet tie |
| AC-030 | Approve an owner-paid expense as reimbursable, then pay the reimbursement | Expense recognized once under chosen policy; owner payable cleared; reimbursement not another expense |
| AC-031 | Record loan principal plus interest/fees, then attempt an excessive or backdated principal repayment | Components separated; negative principal at any affected historical date rejected |
| AC-032 | Open an asset with unknown basis, then approve a value, depreciation, and disposal | Unknown is not zero; no invented depreciation; approved carrying value/proceeds and gain/loss reconcile |
| AC-033 | Save an unpaid supplier bill, pay it, then record a supplier refund | Operational payable changes correctly; cash-method recognition occurs once; refund follows original classification |
| AC-034 | Change a supply from Available to Low/Finished and record expiry; log an actual trip | Correct alerts/history; no invented consumption valuation or $1-per-mile tax deduction |
| AC-035 | Import a statement twice and import an overlapping statement | No duplicated bank lines; preview explains matches/ambiguities; legitimate identical lines remain distinguishable |
| AC-036 | Partially match and combine bank/ledger lines, then attempt an excess match | Correct residuals on both sides; account/direction checks; excess rejected |
| AC-037 | Reconcile a net-zero statement with unexplained +$25/-$25, then resolve items and document real outstanding movements | Initial close blocked; completion only after all items explained and adjusted balances equal |
| AC-038 | Close a period, attempt backdated posting/reversal, then explicitly reopen with a reason | Closed-date writes blocked; reopening logged; old report snapshots retained and revised reports identifiable |
| AC-039 | Reverse a record with active refund/credit/match dependencies | Block until dependencies resolved; approved correction preserves original and restores availability once |
| AC-040 | Post in two months, then request first-month and historical as-of reports | Correct date boundaries/carry-forward; no later receipts/refunds leaking into earlier balances |
| AC-041 | Compare dashboard cards/charts with source reports, including due today and missing due dates | Totals reconcile; aging counts once; unknown dates separate; future bookings never counted as received income |
| AC-042 | Schedule overlapping appointments for one artist and separate work for another | Relevant conflict shown; override requires reason; other artist not falsely blocked |
| AC-043 | Create, restart, approve, and report while disconnected on the actual Surface | Durable local records, usable touch/keyboard paths, PDF creation, pending cloud state; no Google login dependency |
| AC-044 | Switch owner identities and try an unauthorized approval or access to another business/mode store | Authenticated actor accurately recorded; cross-store/unauthorized action denied; no claim based only on a name selector |
| AC-045 | Export Sheets data; interrupt midway; edit an old spreadsheet value | Only complete generations published; desktop totals unchanged by spreadsheet edits; private excluded fields absent |
| AC-046 | Upload while new local changes occur; revoke authorization; exhaust quota/storage; restart | Separate truthful timestamps/watermarks; newer work still pending; bounded retry and useful errors; no paid fallback |
| AC-047 | Back up a busy database with attachment changes | Consistent database snapshot, correct pinned attachment versions, manifest/hash verification, no missing referenced files |
| AC-048 | Restore full backup on another Windows computer without original device credentials | Portable recovery succeeds; counts, IDs, reports, photos, receipts, PDFs, and permissions reviewed; Google reconnect separate |
| AC-049 | Restore with wrong key, corrupted archive, unsafe path, missing file, or incompatible schema; interrupt restore | Clear failure before active-store replacement; original business store remains recoverable |
| AC-050 | Upgrade schema with a failure, resume pending PDF render, and retry document issuance | Recovery snapshot retained; safe migration failure; same issued number/version reused for render retry |
| AC-051 | Review repository, demo mode, logs, PDFs, and export files for private information | Fictional-only public data; no credentials/real records/backups; no TEST/LIVE mixing |
| AC-052 | Record tax charged, collected, refunded, and remitted in different periods under a confirmed test policy | Correct independent period totals and remaining liability; remittance does not mark a return filed automatically |
| AC-053 | Try adding an earlier-business record to the clean new-business setup | No automatic import; explicit evidenced opening entry or rejection; unconfirmed opening remains visible |
| AC-054 | Fill local disk during receipt copy or database save | No false save confirmation or dangling posted attachment; clear error, intact prior state, recoverable temporary files |
| AC-055 | Open a booking without a trial, edit its quantity, then complete/cancel it | Explicit selected plan, count/charge consistency, correct history; no automatic bridal trial or cancellation charge |

## How to validate during development

- Unit checks: exact cents, rounding, boundaries, date rules, participant counts, report definitions.
- Database integration: real SQLite transactions/constraints, retries, stale edits, reversals, and deliberate crash/failure points. Do not substitute an in-memory mock for persistence guarantees.
- Document checks: extract content from generated PDFs and inspect representative rendered pages for layout/privacy.
- Google integration: actual private test destinations, minimal permissions, partial failures, quota/sign-in errors, remote readback. A mocked upload is not proof of recovery.
- Device checks: actual Surface keyboard/touch/offline/restart operation and a second Windows computer for restore.

Before a release, record the application commit/version, schema version, environment, scenario IDs, fixture inputs, expected/actual results, and limitations. Keep real records and sensitive evidence private. Do not mark a scenario passed because its requirement exists in a Markdown file.

## Coverage map

| Area | Acceptance coverage |
|---|---|
| Customers and booking relationships | AC-004 through AC-011, AC-015, AC-042, AC-055 |
| Pricing, documents, schedules | AC-012 through AC-019, AC-050, AC-055 |
| Approvals, ledger, applications, corrections | AC-001 through AC-003, AC-020 through AC-031, AC-039, AC-044 |
| Purchases, supplies, assets, mileage | AC-030, AC-032 through AC-034 |
| Banking, reports, tax, closing | AC-035 through AC-041, AC-052 |
| Offline, backups, privacy, release | AC-043 through AC-054 |
