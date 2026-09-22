# Implementation Plan

Status: Development started. The WinUI preview initializes a local TEST database and preserves its Store ID after restarting on the development computer. An earlier welcome-screen preview launched on the Surface Pro 7. Business features, the remaining local foundation, and further device testing remain pending.

## Current progress

- Development uses the Lenovo computer with VS Code, the .NET 10 SDK, C# tooling, and Windows Developer Mode.
- The [desktop project](../src/SarahBeauty.Desktop/SarahBeauty.Desktop.csproj) has been created and launched with `dotnet run`.
- The welcome screen has been manually checked after its first XAML change.
- The earlier unpackaged, self-contained `win-x64` publish includes all nine image/icon assets, verified against the source files. That welcome-screen copy was manually launched on the development computer and the target Surface; the SQLite additions have not yet been published and tested on the Surface.
- The Surface runs Windows 11 Pro 25H2, OS build 26200.9457. Successful launch was reported by the tester; touch interaction and offline behavior have not been separately confirmed.
- Development now launches unpackaged by default. `AppDataPaths` creates the TEST data directory beneath the current user's local application data directory, outside the repository.
- `Microsoft.Data.Sqlite` 10.0.12 opens `sarahbeauty.db`. Initialization creates `AppMetadata`, preserves an existing `StoreId`, and reads it within a transaction. The preview displays the database path and saved ID, or an initialization error.
- On September 22, 2026, the tester confirmed that closing and reopening the application preserved the Store ID. A read-only database inspection also found `AppMetadata` and the saved ID. This validates the first persistence exercise; it does not complete phase 2 or establish offline, recovery, migration, or Surface storage behavior.
- Schema versioning, stored TEST/LIVE mode validation, owner login, business tables, managed attachments, authenticated approvals, and backups remain unimplemented.
- Phase 1 remains in progress: document the remaining device details and review dependency licenses before closing that phase.

## Agreed decisions

| Area | Decision |
|---|---|
| Device | Native Windows app for a Surface Pro 7; Surface-only initial scope |
| Main records | Local database and managed local attachments |
| Backup | One-way readable Google Sheets export plus complete private Google Drive recovery archive |
| Cost | $0 required recurring software subscriptions; no billing enrollment assumed |
| Users | Two owners sharing one Windows login on the Surface; separate authenticated owner identities inside the app; both can approve, including their own entries, offline |
| Customers | Name-only records permitted; nonblocking contact warning |
| Wedding party | Quantities/placeholders before names; recipients separate from payers |
| Pricing | Bridal $300 including $150 trial; non-bride $120; wedding-only $1 per round-trip mile |
| Deposit | Trial first, then 50% of remaining agreed total; final gets residual cent |
| Payments | Single or split payers; explicit responsibility choices per booking |
| Books | Full books with cash-basis management reports, reconciliation, assets, debt, owners, and tax tracking |
| Supplies | Simple status/opened/expiry tracking; no implied exact consumption valuation |
| Documents | Prepare quotes, invoices, receipts, and credits; manual sending |
| Business start | New business only; earlier activity is not automatically imported |
| Public project | Source, documentation, tests, and fictional examples; private business data stays outside the repository |

## Build order

Each phase is a small set of reviewable pull requests. Build and explain one working feature at a time. Completion means running the relevant acceptance checks, not creating screens or empty tables alone.

| Phase | Deliverable | Exit evidence |
|---|---|---|
| 1. Development setup | Confirm development computer and target Surface, install chosen tools, create solution, add sample window | Application runs on intended Windows target; dependency versions/licenses documented |
| 2. Local foundation | TEST/LIVE separation, settings, owner identity design, SQLite migrations, managed files, local recovery snapshot | Durable offline save/reopen; private-data separation; migration and disk-failure checks |
| 3. Customer book | Customer form/list/search/detail/archive, contact warnings, permission history | AC-004 through AC-006, AC-010/011 and device input check |
| 4. Wedding workflow | Service catalog, bookings, places, appointments, pricing, payment plan | AC-007 through AC-015, AC-042/055; exact rounding and no duplicate trial |
| 5. Documents | Quote/invoice versions and conversion, printable PDFs, receipt/credit templates | AC-016 through AC-019, AC-050; private-field exclusion; actual rendered pages inspected |
| 6. Books and approvals | Chart of accounts, drafts, atomic posting, receipts/applications, fees, transfers, refunds/credits | AC-001 through AC-003, AC-020 through AC-028, AC-039/044; injected failure/retry checks |
| 7. Business resources | Purchases/suppliers, supplies, assets, mileage, owner balances, debt | AC-029 through AC-034; approved values and full ledger integration |
| 8. Reconciliation and reports | Statement import/matching, tax tracking, closing, financial reports, dashboard charts | AC-035 through AC-041, AC-052/053; report reconciliation and historical dates |
| 9. Cloud recovery | Private Google authorization, readable export, encrypted archive, scheduling/retention, restore UI | AC-045 through AC-049; recovery on a second computer; truthful success states |
| 10. Release rehearsal | Installation/update/uninstall behavior, Surface operation, fictional end-to-end business, private live setup | All relevant scenarios executed, unresolved release blockers resolved, known limitations documented |

Local snapshots and private-data protection start in phase 2; cloud recovery is completed before live release. Later phases can refine earlier fields, but changes must preserve issued/posted history and update the relevant docs and tests together.

## Open decisions and setup inputs

These do not prevent documenting or coding independent features. Resolve each before its affected feature becomes live. Keep actual private values in application setup, not this public file.

| ID | Missing decision/input | Required before |
|---|---|---|
| OPEN-01 | Lenovo development setup and published preview launch on the Surface are confirmed. Surface Windows 11 Pro 25H2, build 26200.9457, is recorded; RAM/storage, display scaling, touch, and offline checks remain | Completing the device baseline for phase 1 and relevant device acceptance checks |
| OPEN-02 | One shared Windows profile is confirmed. Choose the app-level owner authentication mechanism, credential recovery, and data-directory protection | Approval/security implementation and live local access |
| OPEN-03 | Any reusable deposit/final due-date defaults, non-bridal payment plans, and override behavior | Applying automatic defaults; otherwise choose explicitly per booking |
| OPEN-04 | Travel payer and unpaid-share responsibility; cancellation/refund/transfer decisions and actual accepted terms | Issuing affected documents or approving each case |
| OPEN-05 | Formation/start evidence, EIN, private business details, own bank/payment accounts and provider setup | Appropriate live setup; not routine development |
| OPEN-06 | Tax registration, sourcing, rates, component/travel treatment, installment allocation, tax timing/rounding, filing frequency | Live tax calculation, affected issuance, and tax-period reporting |
| OPEN-07 | Opening balances, actual owner interests/effective dates, registration expense and payer evidence, asset ownership/value/business use | Opening postings and live financial reports |
| OPEN-08 | Google destination/ownership/access, authorization publishing/testing requirements, free quota/storage fit | Enabling automatic cloud integration |
| OPEN-09 | Backup interval, retention count/age, archive encryption library/format, portable recovery material storage | Scheduled full backups and recovery implementation |
| OPEN-10 | PDF/chart/database access libraries and licenses; Windows packaging, signing, installation/update route | Taking those dependencies or distributing an installer |
| OPEN-11 | Repository license | Claiming reuse permissions or an open-source license; none is selected by this documentation |
| OPEN-12 | Existing workbook/forms cutover and any specifically approved new-business records to migrate | Replacing an existing workflow; avoid two writable books |

Defaults for prices are agreed. The public model supports actual owner percentages but does not publish private financial configuration. Previously supplied setup information must be rechecked when preparing a live store; this document does not certify current legal or account status.

## Git workflow

1. Start a focused branch from current `main`.
2. Make the smallest complete change that can be explained and reviewed.
3. Review its diff and run checks appropriate to its behavior. For documentation, inspect links, examples, consistency, and private-data exposure; don't claim application tests ran.
4. Commit with a concrete description and open a pull request with its purpose and validation.
5. Review the result, fix issues on the same branch, and squash/merge when ready.

Keep personal information, databases, exported sheets, receipts, photos, bank files, tokens, and recovery archives outside the clone. `.gitignore` is a guard against accidental additions, not access control or a way to remove data already committed. Always inspect the staged diff before pushing.

## Release conditions

- Core work and authenticated approvals function offline on the actual Surface.
- Transactions balance, retries are safe, source/target limits hold, and closed periods are enforced.
- Documents reconcile to their sources and reveal only intended client-facing fields.
- Dashboard/report totals trace to committed records and indicate unknowns correctly.
- The complete database, photos, receipts, PDFs, and settings can be restored on another Windows computer.
- Private Google access, quotas/storage, recovery material, local protection, and software licenses are checked.
- Business/tax/opening inputs required for LIVE mode are confirmed, and TEST data remains separate.
- Installation/update preserves business records, and a pre-upgrade recovery path is demonstrated.

Completing the documentation is the starting point for these checks. It is not an assertion that the desktop application is implemented or ready for business use.
