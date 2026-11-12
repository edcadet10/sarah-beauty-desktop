# Build progress

This file tracks the full application build requested on November 10, 2026. A component marked implemented still needs the release checks applicable to it. The app is not ready for live business use.

| Area | Current state | Evidence or remaining work |
|---|---|---|
| Local storage | Implemented | Verified pre-upgrade snapshots, atomic migrations, explicit TEST mode |
| Owner setup and sign-in | Implemented | Two owners, salted PBKDF2-SHA256 hashes, short lockout after failed attempts, expiring store-bound sessions, transaction audit |
| Customers | Implemented | Name-only save, contact warning, validated optional contact fields, search/edit/archive, permission history, stale-edit guard |
| Bookings | Implemented core | Services, recipient places, exact plans, appointments, calendar, paid reservation, and combined cancellation cases/history; complete release walkthrough pending |
| Client documents | Implemented core | Quote versions, bounded payer invoices, dated credit notes, approved-payment receipts, and saved PDFs; full release walkthrough pending |
| Books and approvals | In progress | Posting/reports, applications, funded credits, cash refunds, dated reversals, and retained period close/reopen implemented; remaining workflows and release checks pending |
| Resources | Implemented core | Registers, supplies, reviewed trips/reimbursement links, asset/debt/owner schedules, openings, disposals, bills, mixed supplier payments/refunds, and credits; release walkthrough pending |
| Reconciliation and reports | In progress | Eleven captured financial/booking reports, aging, tax periods, statement correction/matching, reconciliation, bank errors, and period close/reopen implemented; full dashboard, exports, and release acceptance pending |
| Dashboard | In progress | Consistent financial and confirmed-event datasets implemented; remaining operational summaries, charts, and desktop dashboard pending |
| Recovery and Google | Pending | Attachments, portable encrypted archive, Sheets export, Drive authorization/readback |
| Release | Pending | Installer, full fictional walkthrough, actual Surface and second-device checks |

## Owner access checks

The 34 automated storage and access cases pass using fictional temporary stores. Checks include sign-in after reopening, distinct salts, persisted failed-attempt lockout, invalid/repeated setup, cross-store and expired-session rejection, and rollback of business writes. No actual owner names or passphrases are committed.

Storage now lives in `src/SarahBeauty.Business`; tests reference the same compiled project that WinUI uses. Schema 2 introduces Business, Owners, and Audit. Existing schemas 0 and 1 are snapshotted before the upgrade. The interface offers setup, sign-in, and lock/switch owner; interface automation and device checks remain pending.

## Customer checks

Schema 3 adds customer records and permission history. The 40 automated cases include name-only save, contact warning removal/restoration, invalid input rejection, distinct IDs for duplicate names, archive filtering, permission withdrawal history, and stale-edit rejection. The WinUI customer form exposes the same operations. These checks establish service/database behavior; device and interface acceptance checks are still pending.

Password hashing follows [OWASP's PBKDF2 guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) and uses [.NET's PBKDF2 implementation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2). Local sign-in controls application actions; database files also require appropriate Windows account and device protection before live use.

## Pricing checks

The 48 automated cases include the three agreed wedding calculation fixtures, odd-cent deposits, explicit no-trial custom plans, invalid money/overflow rejection, and deterministic proportional cent allocation. Money uses integer cents; quantities use decimal arithmetic. Allocation uses integer arithmetic, including amounts at the supported 64-bit limit. These helpers will be used by booking and document commands.

## Booking checks

Schema 4 adds a shared revisioned store for typed operational aggregates. Booking commands validate referenced customers, named/unnamed counts, retained identities, valid charges, and artist conflicts in the same transaction as the save. Removed unnamed places remain in history. Appointment times carry explicit offsets; overlapping work for the same artist needs an override reason. Status changes retain their actor and reason and never create money movements.

The suite now has 53 passing cases. Added cases cover six unnamed guests with two named later, quantity reduction/increase, explicit tax resolution, no-trial plans, artist overlap/separate-artist handling, and rejection of direct confirmation or cross-business customer IDs. Windows Debug build passes. Booking confirmation is deliberately unavailable until approved deposits can be checked; document, financial, interface, and Surface checks remain pending.

## Financial posting checks

Schema 5 adds accounts, posting batches, immutable movements, journal lines, and book settings. The Money screen prepares drafts, previews the saved journal, and requires the signed-in owner's passphrase before approval. Drafts do not affect posted totals. Both owners can approve their own work or the other owner's work. Posted records have linked corrections rather than editable amounts.

The ledger tests cover the agreed $1,170 cash / $300 asset / $400 debt / $120 result fixture, gross processor receipts/fees/payouts, stale drafts, incorrect passphrases, reliable-reference duplicates, historical principal limits, reimbursement, bounded refunds, exact reversals, closed dates, concurrent approvals, historical reports, and depreciation limits. Injected failures after batch, movement, journal-line, and audit writes roll back the transaction; retries post once.

Invoice applications, customer credit funding, reconciliation dependencies, full asset disposal, period-close/reopen screens, and complete financial workflows are still being built. These core checks do not mark every financial acceptance scenario passed.

## Quote version checks

Schema 6 stores numbered documents, immutable version snapshots, acceptance evidence, and pending render artifacts. The quote screen prepares and reviews saved drafts, confirms the owner before issuance, and records acceptance of an exact version. Issuance retries retain the same number/version. Issuing a proposed revision leaves an earlier accepted agreement intact until the new version is accepted. Unknown tax, unresolved payment plans, changed source bookings, and invalid recipients block issuance.

Three additional cases verify frozen customer/event/pricing data, exclusion of private sentinel fields, same-number issuance retry, version-specific acceptance, and failure without consuming a document number. PDF rendering and invoice conversion are separate follow-up PRs. No document has been automatically sent.

## Invoice conversion checks

Schema 7 adds source coverage, explicit payer headers, invoice milestones, and a billing generation guard. Single-payer decisions cover all charges including travel; split-payer travel needs an explicit charge payer. Unpaid-share responsibility cannot remain undecided at issuance. Partial invoices consume accepted quantities and cents once; their milestone shares sum to the accepted booking plan. Retried/concurrent issuance cannot duplicate coverage.

Five new cases cover full and split-payer conversion, racing requests, tiny fractional-cent allocation, and rejection of an accepted amendment that would reprice billed charges. The fractional-cent probe initially stopped a valid later portion when repeated rounding exhausted the source cents; individual allocations are now capped at their remaining source cents, with an explicit rounding adjustment and final residual. Issued invoice data stays immutable. Applications, credit adjustments, and PDF output remain separate work.

## PDF checks

Issued versions render to private managed PDF files. Rendering has independent Pending/Failed/Succeeded state and can retry without issuing another number. Successful reprints verify and reuse the original file/hash. A missing or changed original is rejected; it is not silently replaced. If file publication succeeds but the database commit fails, an unreferenced file can remain for later maintenance; the operation does not report success.

Four new cases check actual extracted PDF text for totals/privacy, repeated TEST labels and final content across multiple pages, retry after blocked file storage, and rejection of changed/unsafe artifact paths. A fictional six-page quote and invoice were also generated through `tools/SarahBeauty.Demo` and reviewed visually. The renderer uses the pinned MIT-licensed dependency and Windows font resolver documented in [Document dependencies](third-party/dependencies.md).

To create an isolated fictional document rehearsal, run `dotnet run --project tools/SarahBeauty.Demo -- --output NEW_EMPTY_DIRECTORY`. The tool refuses a nonempty destination. Its database, PDFs, and owner fixture are TEST data only. The app still needs full interface, actual Surface, backup/restore, and complete business workflow checks.

## Workspace navigation checks

The workspace now uses the Windows navigation pane. Each section retains its view while switching, so unsaved fields survive navigation. An explicit reload asks before discarding fields. Owner/business/TEST identity and cloud state remain visible. A TEST-only environment override, `SARAHBEAUTY_TEST_DIRECTORY`, allows an isolated generated demo without changing the normal data location; stored mode validation still applies.

An actual developer-computer preview was launched against the fictional demo store. Windows UI Automation signed in, opened Customers, Bookings, Quotes, Invoices, and Money, and verified that unsaved customer input survived a section switch and return. A screenshot of that isolated workspace was inspected. The app remained responsive. This was a developer-machine navigation smoke check, not a full business walkthrough or Surface acceptance pass.

## Payment application checks

Schema 8 adds immutable application history, approval groups, source/target generations, and funding records for the upcoming credit workflow. Saved application drafts require review and owner confirmation. Applications bind approved receipts to invoices and milestones, with explicit decisions when paying on another customer's behalf. Partial application and reversal keep gross and tax components bounded. Source/target changes invalidate stale drafts.

Eight new integration cases cover one receipt across invoices, multiple receipts against one invoice, over-application, dated unapplication before refund, rollback at three write stages, retry identity, concurrent source consumption, tax mismatch, and on-behalf decisions. Historical receipt and invoice balances are checked after each write. A receipt refund or reversal cannot consume value that is still allocated or reserved. Customer credit issuance/application, receipt/credit documents, and booking reservation checks remain subsequent work.

## Funded customer credit checks

Schema 9 makes issued credit/funding history immutable and gives each funding movement an idempotent source draft. A reviewed credit action reserves actual unused receipt value or returns unused credit to the same receipt. Credit applications use the shared allocation/reversal service. Applying or unapplying a credit leaves its underlying receipt reservation in place, preventing use as both customer credit and unused cash.

Seven new cases cover reserve/retry without a second receipt or income entry, two competing $200 uses against $326.65, a single release of unused funding, restored credit after unapplication, rollback at credit/funding stages, and retained tax components. Income and tax are not posted again when credit is used. Cash refunds from reserved credit and charge-adjustment documents remain separate follow-up work.

## Credit cash refund checks

A cash refund can now identify its funded customer credit. Approval releases that credit's reservation and posts the cash refund in one transaction. A dated reversal restores both the cash and the original credit reservation. The receipt never becomes available for a second use as a side effect. Refunds require the original receipt, payer, classification, and bounded unused gross/tax components.

Six new integration cases cover repeated approval, a one-cent excess after partial application, reversal identity, failure between funding and journal completion, mismatched receipt links, and exact tax restoration. All 104 automated cases pass. These checks cover the cash and funding operations; a complete cancellation decision and charge credit note remain separate work.

## Booking reservation checks

The booking screen now offers an explicit owner-confirmed reservation action. It checks the exact accepted event and charges, resolved contact/payer decisions, a dated artist appointment and venue, ready-by conflicts, and approved trial/deposit allocations as of the reservation date. Applied funded credits count toward the same milestones without another income entry. Confirmation saves its accepted quote, required amounts, date, evidence, and actor; retrying the same request does not create another reservation.

Seven integration cases cover trial-only rejection followed by a valid deposit, funded credit, historical date boundaries, unresolved/changed event details, rollback/retry, an explicit no-trial plan, and a ready-by conflict with a recorded override. Reservation history cannot be replaced by editing the booking. This establishes the reservation command; the full cancellation workflow, scheduling interface, and actual Surface checks remain pending.

## Invoice credit note checks

Schema 10 adds immutable, dated invoice reductions. Credit notes retain their original invoice and client snapshot, release bounded source coverage, and reduce unpaid milestone gross/tax balances. Paid amounts must be unapplied before a reduction can consume them. A reduction of an unpaid charge creates no cash, income entry, or funded customer credit. Fully credited charges can be amended and invoiced under a newly accepted quote. Original invoices and PDFs stay unchanged.

Eight new cases cover partial/final-cent reductions, retry, paid-invoice protection and unapplication, later application dependencies, corrected reissuance, stale competing drafts, and rollback at two write stages without consuming a number. Actual PDF extraction verifies the invoice reference, reduction labels, and exclusion of private markers. All 119 automated cases pass, and the Windows Debug build passes. The credit-note screen supports prepare, review, owner-confirmed issue, and PDF creation/reprint.

## Payment receipt checks

Schema 11 links one issued receipt to each approved customer payment. The saved client snapshot shows the original gross payment, recorded method (or an explicit unknown), an owner-reviewed client-safe reference, direct/credit applications, refunds, unused value, and payer invoice balance as of issuance. The snapshot excludes internal memo, account/provider details, evidence IDs, and private customer notes. Changing records after review requires a fresh review; later refunds and allocations do not rewrite the issued receipt or its PDF.

Five added integration cases cover gross versus processor-net value, actual PDF text/privacy, frozen reprints after a later refund, funded credit applications without double counting, review invalidation, duplicate/unapproved/reversed payments, and rollback/retry. All 124 cases pass. A new fictional demo generated quote, invoice, credit note, and receipt PDFs; the receipt and credit-note pages were visually inspected for readable amounts, references, TEST labels, and layout. Windows Debug build passes. No document is sent automatically.

## Resource register checks

The Resources screen now saves and archives suppliers, loans, and assets. Loan opening principal, asset cost, tax basis, and business use can remain unknown. Register saves create no financial movements. Approved financial entries remain separate; a loan's nonzero opening needs a dated ledger opening, and disposed/archived assets cannot receive new ordinary postings. Posted asset basis/use and loan opening metadata cannot be overwritten through these forms.

Four integration cases verify unknown-value preservation, absence of automatic ledger entries, reference/date/contact validation, stale edits, retained archive identity, and protection for posted assets. All 128 cases and the Windows Debug build pass. An isolated fictional Windows UI rehearsal opened Credit notes, Receipts, and Resources and saved a supplier visibly through the form. This developer-machine smoke check does not replace the full Surface/offline walkthrough.

## Opening balance checks

Schema 12 records evidenced opening decisions by account and individual loan/asset/owner. Nonzero openings post through the reviewed Money workflow. A separate Opening balances screen confirms or revokes zero cash openings without creating a financial movement. Unknown stays distinct from zero; register metadata alone cannot satisfy a nonzero loan opening. The account form cannot set the confirmation flag directly.

Six integration cases cover zero/revocation history, duplicate cash openings and reversal, matching loan principal/date before repayment, asset book value without invented tax basis, transactional failure/retry, missing evidence, invalid dimensions/classifications, and dates after the first ordinary movement. All 134 cases and the Windows Debug build pass. These decisions retain source evidence and actors; they do not automatically import an earlier business.

## Supplies and actual trip checks

Resources now includes supplies with Available/Low/Finished status, opened/expiry dates, location, and retained status/date history. Expiry and low/finished alerts are available independently of accounting. Actual trip records identify exactly one real owner or artist driver, route, purpose, distance, parking/tolls, booking, evidence, and reimbursement review status. Neither a supply status change nor a trip log invents a financial transaction or a mileage deduction. Reimbursement payment integration remains pending.

Three new integration cases cover status/date history, inclusive expiry alerts, archives, invalid and stale edits, exactly one scoped driver, and unchanged booking travel/no automatic money posting. All 137 cases and the Windows Debug build pass. One initial test incorrectly compared deserialized arrays by record identity; it now checks the booking revision and charge contents, and the corrected case passes.

## Financial report checks

Reports now provides profit and loss, balance sheet, trial balance with carry-forward, cash movements, customer aging, and tax activity. Each report retains business/mode/basis, date filters, generation time, and committed watermark. Supporting account entries come from that same snapshot. Customer aging separates overdue, due today, future, and unknown dates; unused receipts and funded credits are separate. Tax charged, reduced, collected, refunded, and remitted follow their own document/posting dates.

Four new cases verify aging equality, independent tax periods, historical opening warnings, and a real approval committed midway through report generation in a WAL fixture. The report retains one consistent database generation, including its supporting entries. The full suite has 141 passing cases; the final report changes also pass targeted checks and a warning-free Windows build. An actual isolated Windows UI run displayed the fictional $250 income, $7.50 expense, and $242.50 result. Statement reconciliation, period closing, tax-recovery handling, charting, and the complete dashboard remain pending.

## Asset disposal checks

Money now prepares an owner-approved asset disposal using the recorded asset cost and accumulated depreciation. Proceeds, explicitly resolved included tax, carrying value, and gain/loss remain separate. Zero-proceeds write-offs need evidence. The asset's disposal state and balanced journal commit together; a linked reversal restores them together. A changed asset ledger requires fresh review, and historical disposed intervals block ordinary asset postings even when a later reversal already exists. A buyer can receive an immutable receipt for approved sale proceeds.

Eight integration cases cover gains, losses, write-offs, removal/restoration of cost and depreciation, unknown value, stale review, injected failure/retry, tax/receipt output, remittance dependencies, later asset movements, and future-dated reversals. All 149 automated cases pass, and the Windows Debug build passes. Refund/tax-recovery workflows after remittance still require their own reviewed handling before live release.

## Supplier bill register checks

The Supplier bills screen now prepares line-based purchase drafts, reviews actual tax paid and business/personal shares, and requires owner confirmation before approving the bill record. Supplier/date/reference, expense versus asset classification, individual asset use, booking links, and permanent line IDs are validated. An approved unpaid bill retains its original lines and creates no cash or expense journal entry. Actual payment and supplier-adjustment integration are the next separate feature.

Five new integration cases cover exact mixed expense/asset shares, absence of unpaid cash-method posting, retained approval/retry identity, unresolved tax/use, invalid asset classification, duplicate reliable supplier references, stale review, and rollback/retry. All 154 cases and the Windows Debug build pass.

## Journal asset links

Schema 13 gives individual noncash asset journal lines their own asset identity. Existing movement-level asset history is backfilled after the verified upgrade snapshot. Cash lines stay unassigned. Asset carrying-value limits, disposal calculations, metadata protection, and reversal/generation checks now follow these line identities, preparing for a supplier payment that contains several assets.

Two additional SQLite cases verify a version-12 upgrade with retained balances/links and a real writer batch containing two assets followed by its exact reversal. Both assets restore independently, and an excessive subsequent depreciation fails. This is the posting foundation; mixed supplier payment approval remains the next feature. All 156 cases and the Windows Debug build pass.

## Supplier payment checks

Schema 14 records immutable supplier settlements by original bill line. Money prepares partial/final payments from reviewed approved bills and requires a current bill generation. Business cash recognizes the business expense/asset shares and records any personal share as a draw for its identified owner. Owner contribution/reimbursable funding recognizes only the business shares; reimbursement later clears the payable without another expense. Actual supplier tax paid stays in cost rather than sales-tax liability. Exact reversals restore each bill line and asset identity.

Seven new cases cover partial/final cents, owner funding/reimbursement, historical reversal, simultaneous competing approvals, failure after settlement with retry, supplier report equality, and an owner-funded personal-only installment with no invented business journal. Per-line gross/business availability is validated at all affected dates. All 163 cases and the Windows Debug build pass. Supplier aging and supporting bills use the same report snapshot. The asset-link regression now applies its historical migration SQL directly so later schema additions do not invalidate the old-layout fixture; the original full version-12 upgrade evidence remains recorded above. Supplier refunds and approved bill reductions follow separately.

## Supplier refund checks

Schema 15 preserves signed supplier settlements and links each refund share to its original payment line. Refunds restore the original expense/asset and personal shares, with explicit business-cash, owner contribution/payable, or owner withdrawal treatment. An actual cash refund reopens its bill amount until a separate bill reduction is approved. A paid reimbursement cannot be reduced a second time. Active refunds block reversal of their original payment; reversing a refund restores its source availability exactly once.

Ten new cases cover partial/final shares, business receiving owner-funded refunds, refunds to original owner funding, retained owner withdrawals, reimbursement limits, failure/retry, dependency reversal, future refund bounds, and migration of existing payment/reversal signs. All 173 cases and the Windows Debug build pass. Supplier controls now live in a small separate view class. Approved bill reductions, supplier credit handling, and refunds involving already-depreciated asset carrying values remain follow-up workflows.

## Supplier bill credit checks

Schema 16 protects approved supplier bills and credits from database updates/deletes. The Supplier credits screen records an owner-reviewed reduction against original bill lines, including actual credited supplier tax, business share, source reference, evidence, and date. Unpaid reductions create no cash or expense movement. A paid reduction appears as credit owed by the supplier, separate from unpaid bills; a later actual refund clears it. A linked dated reversal restores the exact reduction without editing the original document.

Eight new cases cover unpaid/paid reductions, refund completion, partial cost/tax shares, stale review, rollback/retry, exact reversal, historical bounds, invalid tax/foreign lines, and database source immutability. All 181 cases and the Windows Debug build pass. Supplier report aging keeps payable amounts and supplier credit separate. Using supplier credit against another bill and handling refunds after depreciation remain follow-up workflows.

A subsequent odd-cent falsification check found that a one-cent unpaid reduction could also consume a business cent already assigned to a retained payment. That check failed with an expected credit business share of zero and an actual share of one. Credit allocation now preserves the paid share and its remaining business/personal bounds; a shared historical check rejects incompatible credit/payment/refund classifications. The corrected case passes. This correction is included in the same supplier-credit PR before merging.

## Supplier funding components

Schema 17 preserves business cost, business-funded personal draws, and outside personal funding within each supplier settlement share. Existing cash-funded personal shares are backfilled. Refunds retain these components and clear a personal draw separately from amounts owed or contributed by an owner. The reusable cent allocator keeps every component bounded and preserves the final residual. The source-consumption link prepares for credit use without also refunding the same payment value.

Three new checks exercise all small component splits, the supported maximum gross, and an actual payment/refund/reversal history through the funding migration. All 185 automated cases and the Windows Debug build pass. This is the funding foundation; the supplier-credit-use command and screen are still pending.

## Supplier credit use checks

Money now transfers existing funded supplier credit to another approved bill from the same supplier, or between credited and unpaid lines of one bill. The action binds its original payment shares and both bill generations. It reclassifies the original cost into the new business/asset shares, with explicit owner funding or retained-draw treatment. It creates no second cash payment or sale. Later use/refund cannot consume the same original value again; exact reversal restores both sides together. Payable lines and supplier credit stay separate even within a single bill.

Schema 18 retains settlement identities while allowing one movement to reference two bills with the same local line ID. Thirteen new cases cover cross-bill/same-bill use, reused line IDs, owner shares, a reimbursed-owner draw and later refund, chained credit use, dependency reversal, rollback/retry, competing $200 uses against $326.65, unfunded/foreign-supplier rejection, and table migration with foreign keys enabled. All 198 cases and the Windows Debug build pass. Full interface, device, backup, and live checks remain separate release work.

## Asset refund recovery checks

Money now records whether an individual asset refund reduces original cost, recovers remaining book value with any excess as a gain, or accompanies the physical return and retirement of the asset. Retirement clears recorded cost and accumulated depreciation, with a separate gain or loss. Evidence and a current asset review are required. The asset state and journal commit together; exact reversal restores them together. Later recovery entries must be reversed before undoing a retirement they depend on. A known zero-value asset can be retired without inventing a monetary entry.

Eight new automated cases cover recovery above carrying value, returned-asset gains and losses, interrupted approval, stale depreciation review, dependent reversals, zero-value retirement, unrelated asset choices, and invalid contra-asset acquisition. All 206 automated cases and the Windows Debug build pass. These choices currently apply to a direct individual asset purchase; selected-line refunds and returns from mixed supplier bills follow separately.

## Selected supplier refunds and asset returns

Supplier refunds can now select original bill lines and gross amounts instead of proportioning every refund across the payment. The selected amounts must be positive, distinct, available, and equal the total refund. The form shows each original line's remaining amount, and the review names the selected items and asset decisions. Default proportional refunds remain available.

Mixed-bill refunds and supplier-credit use now support the same explicit asset recovery and physical-return decisions as direct asset purchases. Several source lines for one physical asset use its book value once. A returned asset cannot also receive the replacement purchase value. Six new integration cases cover equipment-only refunds with untouched supplies, invalid selections, one asset across several lines, replacement funding without another cash payment, physical identity checks, stale review, and rollback of settlement and retirement together. All 212 automated cases and the Windows Debug build pass. Actual Surface and full workflow acceptance remain pending.

## Refunded tax and actual recovery

Schema 19 adds separate sales-tax-recoverable and nonrecoverable-refunded-tax accounts. A customer refund can explicitly split its tax between remaining liability, reviewed recoverable tax, and cost borne by the business. Recoverable/nonrecoverable treatment requires evidence. The default continues to reduce liability and fails if it would make the historical balance negative. The app does not infer recovery eligibility: [Ohio's returned-merchandise and rejected-services rule](https://codes.ohio.gov/ohio-administrative-code/rule-5703-9-11) and [vendor or consumer refunds statute](https://codes.ohio.gov/ohio-revised-code/section-5739.07) describe conditions requiring an actual review.

An owner can approve a later actual cash recovery, an applied liability offset, or an evidenced write-off against that specific refund. Each consumes the original recoverable amount once, retains its review generation, and has an exact linked reversal. Reports show actual customer tax refunds independently of the account used, plus remaining recoverable tax, cash recoveries, liability offsets, and nonrecoverable expense. None of these actions marks a tax return filed.

Ten new SQLite cases cover refunds after remittance, mixed treatment, denial/write-off, cash and offsets, stale approvals, independent claim bounds, dated bounds, dependency reversals, interrupted approval/retry, independent report periods, invalid decisions, and an existing-business upgrade with its recovery snapshot. All 222 automated cases and the Windows Debug build pass. Tax period/filing-status records and live tax setup remain separate work.

## Tax period and filing history

Tax periods now retains confirmed jurisdiction, reporting dates, optional due date, and explicit filing status. New periods are Unreviewed; unknown due dates remain blank. Filed and Amended decisions require the actual filing date, signed reported tax amount, reference, evidence, reason, and owner confirmation. Other reviewed statuses do not claim that a return was filed. A filed period keeps its original reporting dates and jurisdiction, and amendments cannot precede recorded filings. Remittances and recoveries never change these statuses automatically.

Four new integration cases cover independent filing and money records, exact request retries without duplicate audit/history, amendments, unconfirmed details, history protection, overlapping periods, stale decisions, owner confirmation, and failure rollback. The initial test expected $10 cash after an $110 receipt and $10 remittance; its corrected expected balance is $100. All 226 automated cases pass, and the Windows Debug build passes. An isolated fictional desktop run created a tax period, recorded Review required through the form, closed the app, and verified the retained record after reopening. This does not replace Surface or live tax-setup checks.

## Held and unclassified receipt classification

Money now classifies a reviewed portion of a held customer deposit or unclassified receipt as income without another cash receipt. The original cash account, payment method, and known payer stay linked. Tax already collected on a held share carries forward once; newly recognized tax remains separate from income. Ordinary income cannot be classified again. Remaining held deposits appear separately from available income receipts in customer aging.

The classified share can pay invoices, fund customer credit, receive its own bounded refund, or have an exact reversal after dependencies are resolved. Its original source cannot also spend or refund that value. Unclassified money can be returned directly through its suspense classification without inventing income or a customer. A client receipt for a classified portion uses the actual original payment date and explicitly acknowledges a portion rather than another payment.

Nine new integration cases cover invoice and credit funding, held tax and fees, direct unknown-payment returns, source and child refund bounds, stale/backdated approvals, rollback/retry, historical income dates, client-safe receipt content, and a tax-only classification with no invented journal. All 235 automated cases and the Windows Debug build pass. Full cancellation decisions and remaining release workflows continue separately.

An isolated fictional desktop run approved an actual $150 unclassified receipt and then classified $75 through the saved review form. The P&L screen showed $325 income and a $317.50 result, including the earlier $250 demo receipt and $7.50 processor fee. This verifies that form path on the development computer; it does not mark complete Surface or live acceptance.

## Statement CSV preview

Statements now opens or pastes UTF-8 CSV data and previews its mapped dates, descriptions, signs, references, source row numbers, and totals. It supports an explicit signed amount column or separate debit/credit columns, selected date formats, comma/semicolon/tab delimiters, quoted commas, and multiline descriptions. Identical transactions remain separate for review. Malformed quoting, unsupported dates, invalid currency grouping/precision, zero transaction amounts, oversized files/row counts, and amount/total overflow fail before import.

Eleven parser cases pass, and the full suite now has 246 passing cases. The Windows Debug build is clean. An isolated desktop preview retained three fictional rows, flagged two identical rows, and showed the expected $250 inflow, $150 outflow, and $100 net change. File chooser runtime, persistent import duplicate review, matching, and reconciliation completion remain separate work. See [Preview a statement CSV](statements.md).

## Reviewed statement import storage

Schema 20 retains immutable statement headers, bank transaction identities, and original source-row snapshots with their review reasons. Opening balance plus all source rows must equal closing balance. Imports require owner confirmation and a current account generation. Exact request retries return the original result without another audit entry; a separate attempt to import the same content is flagged. Importing statements creates no financial movement.

Overlapping sources can link an existing same-account transaction after explicit review. Equal dates/amounts and repeated source rows require a decision rather than automatic deletion. Distinct real transactions remain distinct. Reliable unique references cannot silently change date/amount or be inserted a second time. One statement cannot link the same transaction twice. An account with imported statements retains its code, type, and cash kind.

Eleven new SQLite cases cover restart persistence, exact retries and conflicting request reuse, overlap/source retention, identical legitimate transactions, account identity, unique references, balance/date/stale errors, immutable records, the version-19 upgrade snapshot, and failure/retry after each write stage. All 257 automated cases pass; the final import cases and Windows build also pass after the account guard change. The import command is implemented; its desktop review controls, bank matching, and reconciliation completion follow separately.

## Desktop statement import review

Statements now confirms the account, period, and balances, captures a review, and shows every source row through pages of 25. Duplicate decisions stay in the review when changing pages. Owners can search existing candidates, explicitly retain a distinct transaction, or link matching unique references with date/amount agreement. Replacing a pending review asks before discarding its decisions. Imported history retains source descriptions, review reasons, transaction codes, owner, and time.

The Windows Debug build passes. An isolated fictional desktop run imported a balanced three-row statement, explicitly retained both identical real purchases, recognized the same CSV on a second review, then reopened the saved statement and its three source rows after app restart. This UI change uses the 257-case tested import command. Bank matching, reconciliation completion, correction of an erroneous import, file chooser runtime, and actual Surface acceptance remain pending.

## Partial and combined bank matching

Schema 21 retains immutable match approval groups, bounded bank/journal portions, and dated partial unmatches. Each pair must use the same cash account and direction, with an effective date on or after both source dates. One bank line can match several cash entries and several bank lines can match one entry. Review generations protect both sides, and historical checks prevent a backdated action from consuming value reserved by a later match. Matching changes no ledger money.

An original cash movement cannot be reversed while its matches remain active at any affected date. Unmatching restores each source once and cannot exceed the original match. Source amounts use actual cash journal cents, including the net receipt after processor fees. Approval owner, time, reason, and exact retry identity remain available.

Thirteen new SQLite cases cover partial/combined matches, grouped excess, direction/account mismatches, partial unmatches, stale and conflicting retries, future reservations, financial-reversal dependencies, actual net cash, all write-stage failures, closed/source dates, repeated maximum-cent match/unmatch cycles, and the version-20 upgrade snapshot with immutable history. All 270 automated cases and the Windows Debug build pass. The matching command is implemented; matching controls and complete reconciliation remain separate work.

A subsequent preview check exposed a review gap: two $70 portions against a $100 bank line passed preview individually even though approval rejected their combined $140. The added preview assertion failed because no exception was raised. Preview now checks the whole proposed dated history as well as each portion. All thirteen matching cases pass with that assertion. The fix is included in the matching PR before merging.

## Desktop bank matching

Bank matching now loads bank/cash sources and their match balances from one database snapshot. Owners can search sources, prepare combined portions, review the captured account/date/reason, approve matching, and partially unmatch an original approval. Source balances refresh after approval, and retained actor/reason history is paged. Source lists include the actual net cash amount rather than customer gross.

A new falsification case showed that a cancellation reversal could be selected as another bank transaction. Matching now excludes both a cancelled cash posting and its cancellation entry, while actual receipts and actual refunds remain independently matchable. The new case passes. All 271 automated cases and the Windows Debug build pass.

An isolated fictional desktop run matched $150 against a $250 bank deposit, then unmatched $50 and verified the $150 bank remainder and $50 cash-entry remainder. A later run through the searchable controls matched $25 and unmatched $10, yielding the expected $135 and $35 remainders. These checks do not complete reconciliation or replace Surface acceptance. Reconciliation completion, erroneous-import correction, period closing, and release work continue separately.

## Confirmed no-activity statements

Schema 22 retains statement evidence references. A manually confirmed no-activity statement can have zero bank rows when evidence and explicit equal opening/closing balances are provided. It creates no bank or ledger transaction. The desktop form requires empty CSV contents for this choice, so a populated net-zero export cannot be hidden by its checkbox.

Three new SQLite cases cover evidenced no-activity and confirmed zero balances, missing/unbalanced inputs, absence of invented money, exact retries, and a version-21 upgrade that preserves legacy request payload hashes. All 274 automated cases and the Windows build pass. An actual fictional desktop run rejected a no-activity claim while CSV rows were present, then saved an evidenced statement with zero source rows and equal balances. Reconciliation closing remains the next separate feature.

## Reconciliation calculation checks

Reconciliation now reads statement, bank, cash, matching, opening confirmation, and posted-correction sources from one database snapshot. It requires every unexplained bank line to be resolved and each outstanding cash remainder to have its full amount, explanation, and evidence. Adjusted statement includes supported deposits in transit and outstanding payments. The ledger closing already includes posted book corrections; the comparison does not add them twice. Unknown opening stays distinct from confirmed zero.

Eight new SQLite cases cover unexplained +$25/−$25 with a zero difference, complete matching, supported outstanding deposits/payments, exact residuals and evidence, posted corrections without double counting, future-date isolation, unknown/future/nonzero openings, foreign/duplicate sources, and cancellation history. Opening entries contribute to book balance but are excluded from new bank transactions. All 282 automated cases and the Windows Debug build pass. This calculation layer does not save a completed reconciliation; completion/reopen commands, retained snapshots, statement-error handling, and desktop controls follow separately.

## Completed reconciliation snapshots and reopening

Schema 23 retains immutable reconciliation snapshots, content hashes, bank coverage, and explicit reopen decisions. Completion requires a fresh reviewed watermark, confirmed opening, zero difference, resolved bank lines, and supported cash remainders. Exact retries return the original approval. Statements continue from the confirmed opening and prior completed closing balance; overlapping sources must agree at that prior date. Prior reconciled coverage is retained rather than counted again.

A completed account/date blocks backdated cash postings, matches/unmatches, opening changes, and newly imported historical bank lines until it is explicitly reopened. Later reconciliations must be reopened before earlier dependencies. Reopening retains the original snapshot and actor/reason history; revised completion creates a new snapshot. Reads check the saved snapshot's content hash. Source metadata lists load independently of full snapshot contents.

Twelve new SQLite cases cover zero-difference refusal with unexplained items, completion/reopen retries, owner confirmation, stale review, immutable sources, dated locks, revised snapshots, continuity that cannot be hidden by an outstanding item, overlapping statements, all write-stage failures, version-22 migration, and deliberate snapshot tampering. All 294 automated cases and the Windows Debug build pass. The older-layout fixture helper refuses to discard newer records or evidence when constructing migration fixtures. Desktop completion/reopening controls, statement-error handling, erroneous-import correction, and period closing remain separate work.

## Desktop reconciliation review

Reconciliation now shows statement and ledger totals, source remainders, outstanding-item evidence, and corrections already posted. Completion uses the captured review watermark and requires owner confirmation. History opens the retained snapshot, including its review reason. An explicit reopening records its owner, time, and reason while preserving the original snapshot. Replacing an unsaved review asks before discarding its selections. Source and evidence tables use pages rather than an unbounded list.

The Windows Debug build and all twelve reconciliation completion cases pass; the latest full suite has 294 passing cases. An isolated fictional desktop run completed a fully matched $100 reconciliation with a confirmed opening, reopened it, completed a revised snapshot, and verified both records after restarting. A separate read-only desktop check displayed the original review reason and reopening actor, time, and reason. These checks ran on the development computer; Surface acceptance, statement-error handling, erroneous-import correction, and full period closing remain pending.

## Statement-error calculation foundation

A separate calculation module supports evidenced errors on the bank side and actual opposite-direction bank corrections. It carries a partially corrected error's remaining amount into the next statement and retains both source claims when the bank corrects an error within one statement. Combined resolutions cannot exceed the original error or the unmatched correction row. It creates no ledger entry. This is a calculation foundation; stored completion, matching reservations, and desktop controls follow separately.

Eight focused cases cover positive/negative errors, partial and complete corrections across months, same-statement correction, combined excess, evidence and identity requirements, foreign or already-matched rows, invalid direction/dates, and maximum-cent arithmetic. The treatment follows the separate adjusted statement and book balances described in [IRS Publication 583](https://www.irs.gov/publications/p583); an owner still supplies the actual bank-error evidence.

## Retained statement errors and corrections

Schema 24 retains immutable bank-error evidence and actual correction portions within the completed reconciliation. Unresolved amounts carry into later periods; full correction leaves the original evidence intact. The original error and each correction reserve their bank portions from the completion date, so they cannot also match business cash. A previously approved future match must be undone before an earlier error can claim its amount. Reopening later dependencies restores availability without changing saved snapshots.

Nine new SQLite cases cover three completed months, a correction sharing a bank row with a real business payment, future and stale matching, reopening dependencies, same-statement correction, failure/retry at each new write stage, and a version-23 upgrade retaining legacy snapshot hashes and request retries. All 311 automated cases pass, and the Windows Debug build has no warnings or errors. The error workflow creates no ledger movement. Its desktop controls and erroneous-import correction remain follow-up work.

## Desktop bank-error review

Reconciliation now prepares evidenced statement errors and links actual bank corrections, including partial corrections to an earlier error. Searchable source lists show at most 50 matches and retain the selected item. Separate small view classes handle error declarations, correction portions, and source search. The preview and retained snapshot show the bank-error adjustment, unresolved amounts, and correction evidence.

The Windows Debug build passes with no warnings or errors. An isolated fictional desktop run completed January with a mistaken $25 bank credit, February after an actual $10 bank correction, and March after the final $15 correction. The expected adjustments were −$25, −$15, and zero; each adjusted statement matched the zero bank ledger. All three completed snapshots and the final review reason remained after restarting. These checks use the 311-case tested backend and do not replace Surface acceptance. Erroneous-import correction, full period closing, and release workflows remain pending.

## Withdraw an incorrect statement import

Schema 25 adds immutable owner-confirmed withdrawal decisions with reasons and evidence. Withdrawing an incorrect import retains its header and original source rows. A bank line remains active when another active statement also references it. Otherwise its matching must be fully undone, including future matches, before withdrawal. Covered reconciliations and book periods must be reopened first. Historical matching remains available for review; withdrawn source lines no longer consume cash availability or appear as match candidates.

The same CSV and reliable references can then be imported with corrected mapping. Active-source duplicate guards remain enforced by database triggers. Retrying the original import returns its withdrawn record without reactivating it. Nine new SQLite cases cover sign correction, reliable references, overlap, historical/future matching, stale owner reviews, exact retries, interrupted withdrawals, retained reconciliation snapshots, and the version-24 upgrade. All 320 automated cases and the Windows Debug build pass. Desktop withdrawal controls follow separately.

## Desktop statement withdrawal and file chooser

Imported statements now show their withdrawal state, owner, time, reason, evidence, and original source rows. The correction form captures the account generation and shows how many unshared lines still have matching dependencies. Withdrawn imports are excluded from the reconciliation selector; matching history labels withdrawn sources while keeping their original decisions available.

The Windows Debug build and all nine withdrawal cases pass; the latest full suite has 320 passing cases. An isolated fictional desktop run loaded a CSV through the actual Windows file chooser, imported it with deliberately inverted signs, withdrew it with evidence, and reimported the same CSV with corrected signs and the same reliable reference. Both sources and the withdrawal reason survived restart. The chooser automation initially targeted a different control with the same numeric ID; narrowing it to the named Open action completed the real chooser check without an application change. This development-machine evidence does not replace Surface acceptance.

## Closed dates for invoices and supplier bills

Two falsification cases showed that invoice issuance and approval of an unpaid supplier bill could change historical customer, tax, or payable reports despite the existing closed-through lock. Both initially failed because the expected rejection did not occur. Invoice and supplier-bill preview/approval now check their actual financial dates. Drafts remain editable, and an exact retry of an already issued or approved record still returns the original result.

All twelve invoice and supplier-bill cases pass with these guards, and the Windows Debug build has no warnings or errors. The latest full suite before this two-case addition had 320 passing cases. Owner-controlled period close/reopen commands and retained report snapshots follow separately.

## Retained book-period reports and reopening

Schema 26 retains versioned, hash-checked financial report snapshots, the reviewed reconciliation identities, and explicit closing/reopening decisions. Closing reads its financial reports in the same database transaction as its reviewed source checks and saved lock. It requires known openings, balanced reports, end-of-period reconciliations for relevant cash accounts, and continuous closing dates that include the initial financial history. Newly introduced statement accounts cannot bypass a closed date. Exact request retries retain the original result, including after reopening.

Reopening requires an owner and reason, proceeds from the latest closed period, restores the previous lock, and retains the original reports. Bank reconciliations remain separately protected until explicitly reopened. Eleven new SQLite cases cover retained/revised reports, unknown and missing inputs, skipped dates, new-account historical imports, stale review, owner confirmation, dependency order, every write-stage failure, exact retries, version-25 migration, and hash tampering. All 333 automated cases pass, and the Windows Debug build has no warnings or errors. The desktop close/reopen review follows separately.

## Desktop book-period review

Book periods now reviews closing checks, captures the financial report set, closes with owner confirmation, and reopens with an explicit reason. History retains the original and revised snapshots, actor names, times, and reasons. Report selection and supporting entries use the captured records. History refreshes and older asynchronous selections cannot replace the result of a newer action. The ordinary Reports screen and captured reports share their table renderer.

The Windows Debug build has no warnings or errors; the tested backend has 333 passing cases. A fresh fictional desktop business was set up through the app. Closing initially showed missing inputs; the same run confirmed three zero openings, imported three evidenced no-activity statements, reconciled cash/bank/processor, and closed the period. Reopening and repeated closing retained three report versions. After restart, the final lock, original review/reopening reason, and captured trial balance remained available. Automation was adjusted to await section reloads and use the expander's supported accessibility pattern. These are development-computer checks; actual Surface and full release acceptance remain pending. See [Close or reopen a book period](book-periods.md).

## Review similar receipts

Saved receipt reviews now show previously posted receipts with the same date and gross amount, including receipts on another cash account. The warning exposes payer, account, description, evidence, approval, and any posting cancellation. It does not delete or reject a distinct real payment. Classifying already received value, expenses, and cancellation entries are excluded as additional cash receipts. Reliable-reference duplicate protection remains unchanged. Matching detail is bounded to 100 records with a truthful total count.

Five new SQLite cases cover independently evidenced equal payments, gross versus net fees, different accounts/dates/amounts, held-value classification, cancellation history, reliable references, and business access. All 338 automated cases and the Windows Debug build pass. An isolated fictional desktop run showed the first $120 receipt and its evidence before approval of a distinct second $120 receipt. Both posted, with $240 income, $7.20 fees, and $232.80 bank/result in the January report. The totals remained after restart. This development-computer check supports AC-022 but does not replace device acceptance.

## Service catalog foundation

The catalog stores configurable service prices, optional durations, archive state, tax review status/evidence, and retained revisions. Initialization adds missing agreed defaults without resetting owner edits or reactivating archived services. New weddings copy the bridal package, guest, travel, and included trial-portion values; the trial portion is not added as a second charge. A custom no-trial booking receives only its selected services and still needs an explicit payment plan.

Booking charges retain the source service revision and copied duration. A source must belong to the same business and name an actual retained revision. Unknown duration remains blank, and a missing price cannot silently become zero. Catalog tax review never assigns a booking tax rate. Existing drafts and issued quote snapshots keep their original values after catalog edits.

Seven new SQLite cases cover the agreed pricing fixture, retained/revised values, issued-quote hashes, no-trial services, stale edits, history protection, invalid definitions/durations, archive behavior, tax review evidence, and business/source-revision boundaries. The full suite has 345 passing cases; the final seven catalog cases also pass after adding tax review metadata. Desktop catalog and booking controls follow separately.

## Desktop catalog and booking service selection

Service catalog now edits prices, optional durations, tax review evidence, and archive state with retained revision history. Changing a selection or creating another service asks before discarding unsaved fields. New weddings use current catalog defaults. Saved bookings can add a selected catalog service, retaining its reviewed revision; a changed catalog selection must be refreshed before use. Adding a service also preserves the edited booking details. Service and trial durations remain editable as explicit optional values.

The Windows Debug build and all seven catalog cases pass, including the new stale-picker check; the latest full suite has 345 passing cases. An isolated fictional desktop run saved a $135/45-minute guest service, copied it into a wedding, changed the catalog to $140/50 minutes, and created a separate no-trial booking with an explicit $60/$80 plan. The older and newer booking prices, durations, venue edits, and plans remained distinct after restart. Another desktop check kept unsaved service fields, discarded them only after the explicit choice, and verified that a new service price stayed blank. Actual Surface acceptance remains pending. See [Manage services and booking prices](service-catalog.md).

## Appointment recipients and duration planning

Appointments can retain permanent named or unnamed recipient-place links. Active work requires distinct active places from its own booking. Overlap checks compare actual customer identities across bookings, so equal names do not merge customers. Recipient overlap needs an explicit override even when different artists are selected. Within one booking, a recorded override covers its related appointment pair; another booking's old override does not approve a new conflict.

A cancellation falsification case initially failed because a cancelled booking still occupied its artist's time. Schedule checks now exclude cancelled bookings while retaining their original appointment records. Historical completed/cancelled work can retain an archived artist; new planned work needs an active artist. Duration planning uses copied service durations for selected places and keeps unknown durations and no-trial plans distinct.

Six new SQLite cases cover cancellation, recipient overlap, named/unnamed identities, foreign/duplicate/retired places, duration planning, and archived artist history. All 351 automated cases pass, and the Windows Debug build has no warnings or errors. Recipient controls, time-zone validation, and calendar review follow separately.

## Event and appointment time zones

Event ready-by times now retain their time zone and any explicitly selected UTC offset. A repeated local time requires its actual offset; a nonexistent clock-change time is rejected. Appointment times with a stated zone must use an offset valid for that date and local time. Legacy offset-only appointments retain their exact instants and an unconfirmed zone rather than gaining an invented zone. Proposed ends use elapsed service duration and convert back through the selected zone across clock changes.

Issued quotes retain the event zone and selected offset. Changing that zone requires a revised accepted agreement before reservation. Client PDFs show the captured zone. Legacy event snapshots retain their original hash and known Eastern label. Implementation follows Microsoft's [ambiguous-time guidance](https://learn.microsoft.com/en-us/dotnet/standard/datetime/let-users-resolve-ambiguous-times) and [invalid-time API](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.isinvalidtime?view=net-10.0).

Ten new cases cover both fall offsets, missing/incorrect offsets, nonexistent times, both duration clock changes, non-hour offsets, saved appointment validation, ambiguous ready-by times, accepted quote changes, PDF labels, and legacy JSON/render hashes. All 361 automated cases pass, and the Windows Debug build has no warnings or errors. Appointment recipient/time-zone controls and calendar review follow separately.

## Desktop appointment planning

The appointment editor now selects permanent recipient places, retains stated time zones, and calculates a proposed end from known copied durations. Unknown zones remain unconfirmed. Event fields expose their time zone and optional ready-by UTC offset. Appointment headers display their explicit offset and recipient-link count. Adding an artist preserves the edited booking details, and historical archived artists remain available on their original records.

The Windows Debug build has no warnings or errors; the backend has 361 passing cases. An isolated fictional desktop run linked two existing unnamed guest places and calculated a 90-minute slot from their copied 45-minute durations. It rejected an overlapping recipient with another artist, then retained the explicit override. A separate trial calculation crossed the repeated clock time from 01:30 −04:00 to 01:30 −05:00. Recipient links, decisions, times, offsets, and edited venue details remained after restart. Calendar review and actual Surface acceptance remain pending. See [Plan appointments](appointments.md).

## Calendar source snapshots

The calendar reads bookings, artists, recipient names, issues, and its watermark from one consistent snapshot. Appointment windows use the selected display zone while retaining the original stated times and zone. Overnight work appears on each occupied date; an appointment ending exactly at midnight does not occupy the following day. Untimed work and undecided event dates remain separate. Artist and cancellation filters do not silently allocate cancelled work.

Seven new cases cover display zones, midnight boundaries, unknown dates/times, recipient overlap and recorded overrides, ready-by issues, cancellation/artist boundaries, concurrent customer edits, private-field exclusion, and an independent all-pairs comparison of 70 varied intervals. All 368 automated cases pass, and the Windows Debug build has no warnings or errors. The calendar screen and actual Surface acceptance follow separately.

## Desktop calendar review

Calendar now captures day, Monday-based week, month, and custom date windows with artist, display-zone, and cancellation filters. Selecting a date opens its agenda; selecting an appointment exposes its source identities, recipient places, original stated times, scheduling issues, and override reason. Untimed work, event-local dates, and undecided event dates stay distinct. Date grids are bounded to 42 dates, and source lists use 100-record pages.

The Windows Debug build has no warnings or errors; the backend has 368 passing cases. A read-only fictional desktop run reviewed two saved wedding appointments, their unnamed recipients and explicit overlap decision, the artist filter, week/month boundaries, and multiple date-grid pages. It displayed a repeated-clock-hour trial in both Eastern and Pacific zones while retaining its two original offsets. Both wedding appointments remained available after restart. The automation's source selection was allowed to wait for the new list to appear. These development-computer checks do not replace Surface acceptance. See [Review the calendar](calendar.md).

## Cancellation source review foundation

A consistent cancellation snapshot gathers issued terms, the recorded event, booking invoices and charges, related receipts, held/classified value, funded credits, and payment applications. Shared receipts expose their allocations to other bookings. Effective-date balances and later dated commitments remain separate; a reviewed classification is labelled as no additional cash receipt. Reading the review does not cancel work or change money.

Six new SQLite cases cover paid terms, shared payments, future applications/credits/charge reductions, held classification chains, posting cancellation dates, closed periods, business boundaries, concurrent edits, and private-field exclusion. All 374 automated cases pass, and the Windows Debug build has no warnings or errors. Combined case decisions, atomic financial effects, and their desktop approval remain follow-up work.

## Credits backed by held customer value

Two initial falsification cases showed that a held deposit or identified suspense receipt could not fund a customer credit. Credits now preserve those original classifications and their tax shares. Funding creates no receipt or journal entry. Invoice allocation still requires reviewed income recognition; an unused held credit share must be explicitly released before its source can be classified. Unidentified suspense cannot create credit for an invented payer. Credit reviews and retained details display the source treatment.

All three new cases pass, including partial refunds that release funding and consume the original receipt only once. The full suite has 377 passing cases; the final 19 credit cases pass after the display changes, and the Windows Debug build has no warnings or errors. A fictional desktop run received a $100 held deposit, reserved a $40 credit, and refunded $20 from it. After restart, the unused credit was $20, bank and held liability were $80, and the original demo income remained $250. Automation was adjusted to select the saved draft by its label and read the multi-line review text. See [Reserve received value as customer credit](customer-credits.md).

## Combined cancellation preview

An explicit case preview combines selected application reversals, invoice charge reductions, actual refunds, funded credit transfers, retained source treatment, and cancellation of booked work. It uses the existing financial writers inside a rolled-back database savepoint, so all combined bounds, dated dependencies, tax, source, and closed-period checks run together without leaving a document number, draft, journal, credit, status, or audit change. Retention creates no automatic fee or income reclassification. A credit's destination can identify a different active booking without inventing an invoice application.

Six new cases cover a mixed taxed outcome, combined excess, missing unapplication, shared receipts, other-booking boundaries, future commitments, held/previously funded value, actual agreement evidence, explicit refund tax review, and complete preview rollback. All 383 automated cases pass, and the Windows Debug build has no warnings or errors. Retained case approval/history and desktop controls follow separately.

## Retained cancellation approval and history

Schema 27 retains immutable, hash-checked cancellation decisions with the original terms, before/after sources, selected actions, owner, and time. Approval confirms the owner, requires the exact saved draft and reviewed business watermark, and commits every financial effect, booking change, case snapshot, and audit together. An identical retry returns its original case. A later evidenced decision remains separate; it cannot overwrite the earlier snapshot. Status-only cancellation leaves any unadjusted invoice obligations visible and creates no automatic fee or refund.

Fourteen new SQLite cases cover a retained mixed outcome, nine interrupted write stages with exact retry, changed sources/drafts and unsaved reviews, owner/business boundaries, competing refunds, no-trial status-only behavior, version-26 migration, immutable triggers, and snapshot-hash tampering. All 397 automated cases pass, and the Windows Debug build has no warnings or errors. Combined desktop case preparation and approval follow separately.

## Desktop cancellation preparation and preview

Cancellation cases now selects a booking and decision date, captures the reviewed agreement, and edits selected application reversals, charge quantities, and retained/refunded/transferred value in separate small form classes. Source selectors search bounded matches and retain existing selections. The combined review reuses paged report tables for its actions, journal, source value, shared applications, and remaining invoice obligations. Unsaved fields require an explicit keep/discard choice before replacement.

The Windows Debug build has no warnings or errors; the backend has 397 passing cases. A fictional desktop run saved and previewed a full $110 unapplication and charge reduction with a $33 refund including $3 tax, $55 credit transfer, and $22 retained. Actual January income stayed $100 and bank stayed $110. The saved draft and proposed outcome survived restart. A separate desktop check reloaded its agreement, kept unsaved fields, and discarded them only after the explicit choice. Automation was adjusted to await list realization and enabled actions after dialogs. Permanent desktop approval/history follow separately. See [Prepare and review a cancellation](cancellations.md).

## Desktop cancellation approval and retained history

The combined desktop review now requires an explicit confirmation and owner passphrase before approval. History opens the retained case with its original agreement, balances, actions, owner, and time, and exposes the linked credit-note PDF. A completed approval cannot replace a newer editor opened while it was running. The booking status panel directs cancellation through this case review.

The Windows Debug build has no warnings or errors; the backend has 397 passing cases. The fictional desktop run required review confirmation and approved the mixed case, leaving January income $70 and bank $77. A separate read-only check confirmed those totals after the report automation initially missed the result. A later $11 refund including $1 tax from the transferred credit left $44 unused credit, January income $60, and bank $66. After restart, current sources showed $44 while the original approved case still showed $55 and produced its linked credit-note PDF. The automation's report refresh wait and null guard were corrected before the final check passed. These checks ran on the development computer; release and Surface acceptance remain pending.

## Actual trip reimbursement review history

Trips retain an optional explicitly reviewed reimbursement request separately from actual distance, costs, and customer-billed travel. Unknown amounts remain blank; a Requested claim cannot silently mean zero. Every save retains the actual details, request decision, evidence, actor, and time. Historical drivers can remain on their saved trips after archive, while new trips require an active driver. Legacy JSON begins history on its next save without fabricated earlier reviews.

Five new cases cover unknown/zero/positive requests, unchanged customer travel and financial totals, retained route/distance/claim corrections, protected history and stale/business boundaries, archived drivers, and older JSON. All 402 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run preserved an unknown claim, an explicit $21.50 request, and a correction from 18.4 to 24.8 actual miles across restart. All three reviews remained; January income/bank stayed $60/$66. Payment links and reimbursement completion follow separately. See [Record an actual trip and reimbursement review](trips.md).

## Dated trip payment limits

A calculation foundation bounds partial and combined reimbursement links against both actual approved expense/owner-reimbursement payments and frozen reviewed claim amounts. Owner reimbursement must identify the actual owner driver. Partial releases retain their dates. A later source return or cancellation cannot invalidate an earlier active link; an affected share needs a release by that date. Changed claim bases cannot overlap, and an old claim identity cannot acquire a different cap. Aggregate calculations use integer arithmetic beyond the single-amount limit before checking bounds.

Seven calculation cases cover shared payments, combined excess, historical source returns and their reversals, full payment cancellation, dated partial releases, changed claims, driver/source/date boundaries, permanent identities, and maximum-cent arithmetic. All 409 automated cases pass, and the Windows Debug build has no warnings or errors. This foundation does not yet save payment links or releases; permanent storage, source-correction guards, and desktop controls follow separately.

## Retained trip reimbursement payment links

Schema 28 retains immutable, hash-checked links to actual approved expenses or owner-reimbursement payments and dated partial releases. Each action confirms its owner, captures the reviewed trip/payment evidence and business watermark, and retries without duplicate attribution. Owner reimbursements must identify the actual owner driver. Linking or releasing creates no additional expense, payable, or cash movement.

Active links protect the trip's driver, actual basis, and reviewed amount. After release, an explicit trip revision and new link retain both claim bases. A source payment return or cancellation needs sufficient dated releases first, including during financial preview. Trip actions proceed in date order; historical aggregate bounds still protect shared payments across trips and future source corrections. Releasing attribution does not refund cash.

Eleven new SQLite cases cover partial/many-to-many links, source and claim excess, stale/competing reviews, owner-driver/source/evidence/date/business boundaries, source returns/reversals, revised claims, four injected write failures and retries, version-27 migration, downgrade protection, immutable triggers, and hash integrity. All 420 automated cases pass, and the Windows Debug build has no warnings or errors. The trip screen's payment/release controls and completion review follow separately.

A formatting falsification then showed that `18.40` miles was incorrectly treated as a changed basis from `18.4`. The claim fingerprint now uses a canonical numeric value, keeping the exact distance while removing insignificant decimal scale. The reviewed source still retains the entered value. This follows .NET's [general numeric format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#general-format-specifier-g). The added equivalent-value edit passes with the eleven trip-storage cases; the complete suite immediately before this edge-case addition had 420 passing cases.

## Desktop trip reimbursement review and completion

Saved trips now review and link actual approved payment portions, show current versus earlier claim bases, and retain source details and dated releases. The trip editor's unsaved fields are preserved and must be saved before a link/release review. Changed decision fields cannot approve an older captured review. Older asynchronous results cannot replace a newer selection. Link and release histories use paged tables and identify their owners.

Three new SQLite source cases cover dated partial release, separation of earlier claim bases, and a concurrent link without mixed rows or watermarks. All 423 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run approved one $21.50 expense payment, linked $10 and $11.50, released $5 of attribution, and linked that $5 again. It rejected unsaved parent fields and a changed captured amount. A formatting-only change from 24.8 to 24.80 miles retained the active claim basis. After restart, three links and one release netted to $21.50, with January result/bank $38.50/$44.50 and one actual expense payment. These development-computer checks do not replace Surface/release acceptance. See [Record an actual trip and reimbursement review](trips.md).

## Reviewed asset register facts

Report preparation exposed a blocked path: registered cost and tax basis could remain unknown after capitalization, while ordinary editing correctly protected posted asset history. An evidenced owner-confirmed fact review now completes or corrects those current register values separately from the approved ledger. It retains previous/new values, reason, evidence, actor, and time; exact retries do not reapply an earlier value. Ordinary edits cannot replace a review. The desktop compares a captured decision and preserves newer or unsaved editors.

Five new cases cover completion after capitalization, corrected facts without journal changes, zero/unknown distinctions, old-request retry, protected history, stale/owner/evidence checks, and two interrupted write stages. All 428 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run capitalized a $50 asset with unknown register facts, completed its cost and tax basis through owner confirmation, and retained a later tax-basis correction from $40 to $35. It rejected an edited captured decision. After restart, both reviews remained, equipment stayed $50, and January result/bank stayed $38.50/$14.50 after the actual purchase/funding. See [Review registered asset cost and tax basis](asset-facts.md).

## Asset schedule snapshots

Financial reports now capture individual asset posted openings, cost increases/reductions, accumulated depreciation added/cleared, closing book value, and dated disposal state. Current register facts remain separate from approved accounting values. Missing book history stays unknown; a fully reduced asset remains known zero. Supporting movement identities include zero-value retirement decisions, and a shared payment's cash is not automatically divided among assets.

Six new SQLite cases cover disposal/reversal periods, unknown versus zero, mixed supplier returns, archived/current facts, legacy report JSON, concurrent posting, and two assets on one payment. The closing-report check also confirms that a later register review cannot replace captured facts. All 434 automated cases pass, and the Windows Debug build has no warnings or errors. Desktop schedule selection follows separately. See [Review the asset schedule](asset-schedule.md).

## Desktop asset schedule and source review

Reports now offers the asset schedule with paged book values, period movements, separately labeled captured register facts, and individual supporting entries/events. Saved book-period review uses the same captured schedule. Older closing reports explicitly show that a schedule was not captured instead of loading current business records.

The Windows Debug build has no warnings or errors; the backend has 434 passing cases. A read-only fictional desktop run confirmed a $50 closing cost/carrying value, a $50 period increase, and a separate $35 registered tax basis with its acquisition sources. The same review passed after restart. An earlier retained closing report displayed the missing-schedule message. The automation was adjusted to await expanded table realization before checking rows. These development-computer checks do not replace Surface/release acceptance. See [Review the asset schedule](asset-schedule.md).

## Captured loan lender facts

A failing-before SQLite check confirmed that ordinary loan editing could reassign posted debt to an owner. The owner-lender link is now protected after posting. New saved loan openings, proceeds, and repayments capture the reviewed lender/name and owner link; changed lender facts require a resaved draft before approval. Exact retries and reversals retain original identities. Older approved movements remain unchanged and must not acquire invented historical attribution.

Four new cases cover protected reassignment, changed/forged/missing facts, evidenced openings and repayment components, exact retry, reversal, renamed current facts, and business boundaries. All 438 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run approved a $100 owner loan and a $35 repayment split into $30 principal, $4 interest, and $1 fees. The owner selector became protected; changing the current display name retained the approved source name. The original identity and January result of -$5 remained after restart. These development-computer checks do not replace Surface/release acceptance. See [Retain a loan's lender identity](loan-lenders.md).

## Debt schedule and captured source review

Reports and retained book-period review now offer individual loan principal openings/closings, opening adjustments, proceeds, repayments, other principal changes, and separate interest/fees. Period activity includes dated reversals. Current register facts remain separate from captured lender identities. Missing approved principal history stays unknown; fully repaid approved principal is known zero. Older retained reports explicitly identify a missing schedule.

Five new SQLite cases cover dated repayment/reversal components, future approved openings, separate and archived loans, current versus original lender names, unknown/zero distinctions, concurrent posting, legacy report JSON, and older unconfirmed lender attribution. All 443 automated cases pass, and the Windows Debug build has no warnings or errors. A read-only fictional desktop run showed $100 proceeds, $30 principal paid, $4 interest, $1 fees, and $70 outstanding; a date before the first approved entry remained unknown. Current and original lender names stayed separate after restart. An earlier closing report displayed the missing-schedule message. These development-computer checks do not replace Surface/release acceptance. See [Review the debt schedule](debt-schedule.md).

## Owner schedule and actual attribution

Reports and retained book-period review now separate each owner's contributions, draws, reimbursement payable changes, actual reimbursements, and loan activity. Individual owner-account roll-forwards retain approved openings and supporting source identities. Inactive owners remain in history. No ownership percentage divides transactions, and posted balances do not certify tax capital or basis. A loan's closing principal enters an owner's confirmed subtotal only when every supporting principal entry captures that same lender; older attribution stays separate and unconfirmed.

Five new SQLite cases cover unequal two-owner contributions, asset contributions, draws, owner-paid costs/reimbursements, loans, inactive owners, dated openings/reversals, mixed supplier funding and personal benefit, older loan attribution, concurrent posting, and legacy report JSON. All 448 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run retained Alex/Morgan contributions of $20/$10, Alex's $5 draw, $8 owner-funded business cost, $3 reimbursement, $5 remaining payable, and $70 owner loan. January result remained -$13; both owners' balances and sources survived restart. An older closing report displayed the missing-schedule message. These development-computer checks do not replace Surface/release acceptance. See [Review owner activity and balances](owner-schedule.md).

## Shared document source integrity

Booking report preparation found that PDF rendering checked a retained document's hash while shared report/source readers did not. Two failing-before cases confirmed that changed JSON or a changed stored hash could enter financial reports. The shared document reader now verifies the original stored JSON bytes before deserialization. Quotes, invoice/credit workflows, reports, cancellation sources, and artifact status/rendering share that check; the duplicate rendering-only query was removed.

The two mismatch cases now pass across reports, quote history, artifact status, and cancellation sources. A third case verifies valid original JSON without reserializing its shape or whitespace. All 451 automated cases pass, including existing legacy-zone document coverage, and the Windows Debug build has no warnings or errors. This change does not rewrite approved document snapshots.

## Booking billing source snapshots

A consistent billing snapshot now retains each booking invoice's original payer/lines, source quote identity and hash, linked credit notes, applied receipt/credit value, and remaining milestone balances. As-of dates exclude future invoices, notes, and unapplications. Effective net charges and tax must reconcile to applied plus remaining value. Current booking names/revisions remain separate from frozen issued facts, and private notes are excluded.

Four new SQLite cases cover shared receipts, funded credit applications, dated partial unapplication/reduction, a fully credited older invoice and revised reissue, future documents, changed payer/booking names, private/business boundaries, read-only watermarks, and concurrent credit issuance. The revised-agreement case retains $120 net charges without subtracting the older $100 credit twice. All 455 automated cases pass, and the Windows Debug build has no warnings or errors. Direct-cost/contribution calculation and the desktop booking report follow separately. See [Review booking charges and contribution](booking-contribution.md).

## Booking financial source snapshots

A consistent financial source snapshot now separates a booking's explicitly linked direct expense lines, recognized service income, and receipt/refund gross, tax, fee, and net amounts. Direct costs exclude principal, funding/draws, asset capitalization, financing costs, depreciation, and asset disposal results. Held receipts remain separate from recognized service income. Dated posting reversals retain the original tax and fee amounts; they are book corrections rather than new physical customer payments. A shared payment application does not invent a booking tag or another receipt.

Five new SQLite cases cover mixed service/held receipts and cost/funding categories, dated refunds and both posting-cancellation directions, shared untagged receipts, supplier return attribution and reversal, private/business/date boundaries, and concurrent expenses/watermarks. All 460 automated cases pass, and the Windows Debug build has no warnings or errors. Combined contribution calculation and desktop reporting follow separately. See [Review booking charges and contribution](booking-contribution.md).

## Combined booking contribution and source review

Reports and retained book-period review now combine issued-charge estimates, recorded direct costs, cash-method service income, receipt/refund amounts, applied value, and remaining payer balances without mixing their bases. Unassigned costs remain separate. A booking without issued charges has no estimate; cancellation alone does not erase an invoice. Current service charges, recipients, and appointments are captured separately from immutable invoice/credit lines. Private work notes and products/shades are excluded.

Four new SQLite cases cover the separate estimate/cash/income/application amounts, unassigned expenses, unissued work and operational privacy, cancelled/archived bookings, preserved captured estimates and legacy JSON, and concurrent charge/cost changes with matching watermarks. All 464 automated cases pass, and the Windows Debug build has no warnings or errors. A read-only fictional desktop run showed $0 remaining cancellation charges/estimate/due separately from $60 recognized income and $66 receipt/refund cash, with original payer, no-trial status, $100/$10 credit reduction, and later $11/$1 refund. January result stayed $38.50 after restart. Another unissued booking kept its copied $135/$270 guest pricing, unnamed places, preserved venue, and repeated-hour offsets. An older closing report displayed the missing-report message. These development-computer checks do not replace Surface/release acceptance. See [Review booking charges and contribution](booking-contribution.md).

## Captured tax filing reviews in reports

Tax activity now captures relevant registered filing periods and full reviewed history alongside financial tax figures. Filing status is explicitly labeled at capture time; its filing dates remain distinct from the financial as-of date. Period overlap includes quarters and archived records, while unknown due dates and unrecorded amounts remain explicit. Later amendments cannot replace captured original filings, and older saved reports identify missing filing snapshots.

Three new SQLite cases cover payment/status separation, original and amended reported amounts, snapshot/legacy preservation, period boundaries/jurisdictions/archive, and concurrent status/history/watermark changes. All 467 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run captured a filed zero and later reported -$1 amendment with their February dates and unknown due date in the January report. Collected/remitted/liability stayed $0. Both reviews remained after restart, and an older closing report displayed the missing-review message. These development-computer checks do not replace Surface/release acceptance. See [Review tax activity and filing records](tax-report.md).

## Dashboard financial datasets

A single financial snapshot now supplies dashboard monthly income/expense/result, expense categories, aging bands, cash balances, and their supporting sources. Funding/principal remain outside P&L, depreciation stays explicit, due today stays Not due, and each remaining invoice share is counted once. Cash retains opening confirmation and the latest active reconciliation on or before the financial as-of date, with verified saved hashes. Empty months remain gaps unless approved activity or verified closed-book coverage supplies context; reopening a no-activity month removes its known-zero coverage. Windows support at most 120 calendar months and retain partial-month dates.

Six new SQLite cases reconcile mixed funding/cost/disposal activity and cash, all aging boundaries and partial payment, closed no-activity months and independent bank reopening, concurrent posting/watermarks, date limits through year 9999, and corrupt closing/reconciliation snapshots. All 473 automated cases pass, and the Windows Debug build has no warnings or errors. Operational summaries, charts, and the desktop dashboard follow separately. See [Review the business dashboard](dashboard.md).

## Amended reservation review

Dashboard preparation exposed a blocked path: a Confirmed booking could accept an amended quote, but reservation confirmation still required Awaiting deposit. A failing-before case reproduced the rejection after its additional deposit was funded. Confirmed bookings can now review a changed accepted agreement, with full event/schedule/funding checks and chronological reservation dates. The review appends its exact agreement and required amounts without a fake same-status transition or another cash movement. Same-request retries, including GUID case variants, retain their original result; duplicate reviews of the current agreement are blocked.

The desktop offers the changed-agreement action and paged reservation history, blocks unsaved parent fields, and preserves newer/changed editors after a late result. The optional fictional demo seeds a confirmed original agreement and pending additional deposit. Three new cases cover additional funded milestones, prior confirmation dates, interrupted writes, retry identity, unchanged money/history, and duplicate current reviews. All 476 automated cases pass, and the Windows Debug build has no warnings or errors. A fictional desktop run rejected unsaved fields and insufficient additional funding, received/applied the actual $30, and retained original $150/$75 versus amended $150/$105 trial/deposit reviews. Read-only restart verification found both reviews; bank/result stayed $255/$497.50 after the actual additional payment. The automation's initial history check was adjusted to wait for the new editor after approval. These development-computer checks do not replace Surface/release acceptance. See [Review a changed booking reservation](reservation-reviews.md).

## Dashboard confirmed-event datasets

A consistent planning snapshot now counts active registered Confirmed weddings in a separate dated window, with undated work separate. Accepted agreement totals include recorded tax and remain separate from income/cash. Unaccepted price edits do not inflate them. Current agreement/schedule/reservation/funding issues and archived/unavailable artist identities stay visible. A current-agreement credit adjustment keeps its exact commitment outside the known subtotal until reviewed or fully reissued to that agreement's net/tax amounts; reissued value is counted once. Payer balances use the separate planning as-of date.

Six new SQLite cases cover accepted versus unaccepted/future value, credit/reissue lineage, cancelled/completed/archived/window boundaries, undated and archived-artist issues, private-field exclusion, concurrent cancellation/watermarks, store boundaries, and year-9999 dates. All 482 automated cases pass, and the Windows Debug build has no warnings or errors. Remaining operational summaries, charts, and desktop presentation follow separately. See [Review the business dashboard](dashboard.md).

## Dashboard operational datasets

The dashboard now captures saved approval drafts, missing invoice due dates, active booking and proposal reviews, incomplete setup, unclassified receipt value, trip reimbursement balances, and active supply alerts. Reserved suspense credit remains unclassified. Dated actual trip payment links clear a paid request; releases restore only the unpaid portion. Explicit zero-opening and filing decisions clear their own tasks without inventing other missing values. Private supply/work notes stay outside the snapshot.

The combined reader loads financial, upcoming, and operational datasets in one database transaction with matching watermarks. Six new SQLite cases cover read-only drafts/dues, reserved unclassified credit and release, partial/full trip payment attribution with dated release, inclusive expiry and privacy, explicit setup decisions, and concurrent changes across cash, bookings, supplies, and drafts. All 488 automated cases pass, and the Windows Debug build has no warnings or errors. Charts and desktop presentation follow separately. See [Review the business dashboard](dashboard.md).

## Desktop dashboard cards and source review

The placeholder dashboard now captures dated financial, commitment, and operational cards together. Expandable sources retain payer aging/milestones, current artists and agreement identities, cash openings/reconciliations, pending work, supplies, and owner/debt/tax schedules. Account review uses cumulative cash entries or period entries as labeled. Section buttons update the navigation selection and preserve cached editors. Captured dates remain explicit; invalid or changed loading inputs preserve the previous result. Backup health states the unconfigured export/archive/restore status.

The latest complete suite remains 488 passing cases; the Windows Debug build has no warnings or errors. Fictional desktop checks reconciled January income $60, expenses $21.50, result $38.50, and bank $14.50 to sources, including cumulative cash versus period income. Invalid dates preserved that capture, and section routing selected Reports. Read-only restart verification retained the same results. A separate fixture displayed one amended confirmed wedding at $360 including tax, bank $255, and remaining $75/$30 milestones; its routing selected Bookings. These development-computer checks do not replace Surface/release acceptance. Charts follow separately. See [Review the business dashboard](dashboard.md).

## Native dashboard charts

The dashboard now renders clustered monthly income/expense columns, a monthly result line, aging bars, confirmed-event value/counts, expense-category bars, and separate owner contribution/draw/loan columns. Exact tables accompany the approximate plots. Missing financial context breaks the result line; verified closed no-activity months retain known zero. Negative values remain signed. Upcoming tax-inclusive commitments use their separate window and exclude unknown values from the known subtotal. Native WinUI shapes add no chart dependency.

The latest complete suite remains 488 passing cases; the Windows Debug build has no warnings or errors. Read-only fictional desktop checks verified November $250 income/$7.50 expense/$242.50 result and January $60/$21.50/$38.50 with December/February gaps, dated aging and expense accounts. Separate fixtures verified a -$13 result; Alex/Morgan contributions $20/$10, Alex draw $5, owner-loan proceeds $100/principal paid $30/closing $70; a closed zero January followed by an unknown February; and one $360 confirmed event in its correct month. These development-computer checks do not replace Surface/release acceptance. See [Review the business dashboard](dashboard.md).

## Versioned managed evidence storage

Schema 29 retains immutable attachment metadata and version chains. The service copies supported PDF/JPEG/PNG bytes to generated private paths, records size/content/metadata hashes, checks latest versions, and retains earlier copies. Exact retries return the existing version; conflicts require a new request. Staged copy/publication failures do not advertise a successful record. Uncertain commit cleanup preserves potentially referenced files. Reads verify a held file stream and managed path boundaries.

Customer photos require current Granted permission with the exact recorded use/scope, retain the original permission identity, and recheck current permission when read. Withdrawal blocks content access without deleting history. Thirteen new cases cover original removal, versions/retries, copy/publication/metadata interruptions, changed content/metadata, photo permission/withdrawal, type/size/cancellation/business boundaries, unsafe paths, and upgrading a version-28 store with verified prior snapshot and retained customer. All 501 automated cases pass, and the Windows Debug build has no warnings or errors. Record links and desktop import/open follow separately. Complete encrypted archives and device restore remain pending. See [Keep managed evidence files](managed-evidence.md).

## Pinned evidence links and correction history

Schema 30 retains immutable associations to supported, existing business records and explicit reasoned releases. Links pin an exact attachment version/hash and capture target type/identity/name/revision/status, purpose, actor, and time. Later file versions or record edits do not retarget them. Photos link only to their own permission-reviewed customer. Current permission is checked on linked content reads; corrections remain possible after withdrawal without opening the image. File/target review, duplicate protection, and exact retries run inside the decision transaction. No link or release posts money or rewrites approved financial sources.

Eight new cases cover version/target pinning without financial changes, stale/type/business boundaries, photo restrictions/withdrawal, interrupted link/release decisions and retries, changed decision hashes, original pinned metadata hash, and version-29 upgrade preservation. All 509 automated cases pass, and the Windows Debug build has no warnings or errors. The dashboard cards and charts are merged. Desktop evidence import/open follows separately; complete encrypted recovery and device checks remain pending. See [Keep managed evidence files](managed-evidence.md).

## Desktop managed evidence workspace

Evidence files now provides native file selection, named/reasoned imports, exact version/history review, integrity verification, explicit PDF opening, private scaled image preview, replacement versions, typed/revisioned record associations, and reasoned releases. Photo permission is checked for each content open; navigation clears an existing image preview. This workspace guards unsaved import/association fields and active writes, preserves newer fields after late results, and distinguishes a completed import from a later history-refresh failure. Global editor hardening remains separate work.

The latest complete suite remains 509 passing cases; the Windows Debug build has no warnings or errors. Fictional desktop checks imported/verified receipt v1 with the actual Windows picker, linked it to the existing approved card payment, and added v2 without moving the v1 link. After moving the original and restarting, v1 and its link still verified; income/expense/result/processor stayed $250/$7.50/$242.50/$242.50. A modified managed PDF was rejected with a recovery action. Photo checks preserved unsaved input, rejected import before a grant, imported/linked/privately previewed after the exact grant, cleared preview on navigation, blocked reopening after withdrawal, and retained a reasoned release after restart. These development-computer checks do not replace Surface/recovery acceptance. Entry-form selection and encrypted recovery follow separately. See [Keep managed evidence files](managed-evidence.md).

## Owner confirmation without a long writer reservation

GitHub caught a database-lock timeout in the existing competing supplier-credit approval test. A local failing-before gate reproduced the underlying contention: pausing passphrase verification inside its writer transaction blocked an unrelated customer save with SQLite Error 5. Verification now runs outside the writer reservation. The short result transaction rereads current credential material, active owner/business, failures, and lockout before accepting or recording the result. Hash work and persisted lockout requirements are unchanged.

Five new cases cover available business writes during verification, changed credentials/deactivation, concurrent failure counts, and a lockout recorded during an earlier valid proof. All 506 main-based automated cases pass, including the existing supplier-credit race; the Windows Debug build has no warnings or errors. Password recovery/change, automatic idle UI lock, and restore-instance invalidation remain separate work. See [Confirm owner access](owner-access.md).

## Managed evidence in financial sources

Money now offers explicit saved receipt/evidence version selection alongside existing decision notes. Draft saves validate the captured file facts; preview and approval also verify its bytes. Approved sources retain the exact version/name/category/size/content and metadata hashes. Replacement files do not retarget a source. Customer photos are excluded in the picker and rejected by the business service. Older absent references retain their JSON shape and remain unconfirmed. Corrections can have separate evidence without reopening missing originals or rewriting original sources.

Schema 31 raises the minimum writer contract so older models cannot silently discard the new reference or bypass its approval check. Eight new cases cover pinned sources/notes and money, forged/foreign/photo references, missing/changed bytes, rehashed metadata, correction independence, legacy text/JSON, and version-30 upgrade preservation. All 522 combined automated cases pass, including the authentication writer-lock fix; the Windows Debug build has no warnings or errors. Fictional native checks selected/verified v1 while v2 existed, approved exactly one $10 expense, and retained that file and notes after restart and schema upgrade. January income/expense/result/bank remained $0/$10/-$10/-$10, with earlier processor $242.50. These development-computer checks do not replace full release/device acceptance. See [Select a saved file for a money entry](financial-evidence.md).

## Portable recovery encryption boundary

The recovery helper now uses the published age v1 format through its pinned 1.3.2 Go library. Native hybrid recovery identities stay in memory; returned identity files are passphrase-protected with the library's scrypt policy. Archive encryption uses only the public recipient. The bounded standard-input protocol keeps passphrases out of process arguments and returns fixed safe errors. New output files are exclusive; ordinary failed decryptions remove incomplete plaintext. Source/build definitions and third-party notices are retained with the project.

All four crypto test groups pass with fictional data, including standard reference CLI interoperability in both directions, empty/exact-chunk/multiple-chunk streams, actual output size/hash, wrong passphrase/identity, changed/truncated encrypted files, existing-output preservation, protocol bounds/redaction, and excessive recovery work rejection. The latest complete .NET suite remains 522 passing cases with a clean Windows build. Full database/file archive assembly, staged restore validation, recovery UI, and actual device recovery remain pending. See [Encrypt portable recovery data](archive-encryption.md).

## Desktop recovery process bridge

The business assembly now calls the bundled helper through bounded local pipes with private inputs off the command line. Each operation owns a temporary output directory. The client waits for a complete result, independently verifies file size/hash, and publishes without overwriting another destination. Cancellation/deadlines terminate and drain the process before cleanup. Fixed safe errors cover wrong recovery material, unavailable inputs/outputs, unsupported sizes, missing helpers, and malformed results. Build output includes the helper and dependency notices; CI builds it before storage checks.

Five new .NET cases exercise the actual Windows helper, protected material inspection, multiple-chunk round trips and independent hashes, wrong passphrase/truncation with no published plaintext, safe error text, cancellation/timeouts and cleanup, concurrently created destinations, wrong output hashes, oversized input, and missing-helper guidance. All 527 .NET cases pass; all four Go crypto groups also pass. The Windows Debug build has no warnings or errors and contains the helper. Full archive assembly and staged restore follow separately. See [Encrypt portable recovery data](archive-encryption.md).

## Consistent recovery source capture

Recovery capture now creates a verified standalone SQLite snapshot in its own working directory, then copies every recorded successful PDF and immutable managed attachment version referenced by that snapshot. Business/mode/schema/watermark, table counts, exact aggregate journal sides, and file paths/sizes/hashes come from that copy. Later live changes stay outside it. Retained private photo history is copied as private recovery data; normal content permissions remain unchanged. Unsafe/duplicate paths, failed retained hashes, database/FK problems, missing/changed files, or an unbalanced posting batch block completion. Ordinary cleanup removes only known generated workspace files.

Ten new cases cover independent database/PDF/file copies, earlier/private versions, committed WAL and later writes, missing/changed content, changed metadata, unbalanced journals, unsafe paths/foreign sessions, interrupted stages, and cancellation after an empty-store snapshot. Two checks caught regressions before publication: the snapshot refactor had created a folder before rejecting a missing source, and an empty-store capture had ignored cancellation after the database step. Both now pass. All 537 .NET cases pass, the four Go groups remain passing, and the Windows Debug build has no warnings/errors. Package encryption, staged restore, and recovery UI follow separately. See [Capture a complete recovery source](archive-snapshot.md).

## Complete local encrypted archives

The archive service now packages the consistent captured database, successful issued PDFs, and every retained managed-file version with their manifest. It verifies file bytes during ZIP creation, checks the completed package while holding it against modification, encrypts with only the reviewed public recipient, independently checks ciphertext size/hash, and publishes to a new destination. Its result retains exact manifest/ciphertext hashes, recipient fingerprint, source watermark, and journal facts. Later live changes remain outside that archive. Cancellation and interrupted stages preserve the source and existing outputs.

Eleven new fictional .NET cases use the actual helper, decrypt the completed archive, check every entry/hash, reopen the frozen database, compare counts/watermark/money, and exercise later changes, interruption/cancellation, changed captured/package bytes, foreign sessions, invalid recipients, and existing destinations. A test's expected invalid-recipient code was corrected to the helper's established UnsupportedRecipient contract. A repeated helper build exposed inherited read-only license files; copying notices now supports rebuilding. All 548 .NET cases and four Go groups pass; the Windows Debug build has no warnings/errors. Staged restore, recovery UI, scheduling, and actual device checks follow separately. See [Create a complete encrypted archive](archive-package.md).

## Isolated restore validation

The restore service now holds and hashes selected ciphertext, optionally compares a completed backup record, decrypts into a generated workspace, and validates a bounded manifest before copying only canonical managed paths. Duplicate/link entries, unsupported metadata, extra/missing files, size/hash differences, and expansion beyond recorded bytes are rejected. The read-only restored database must match identity/mode/schema/business/watermark, integrity/FKs, retained source hashes, journal balance/totals, table counts, and every managed file reference. A disposable review retains the verified facts; it does not activate another writable store or reset owner access.

Twenty-eight new fictional cases cover exact recorded/history-free review, active-store independence and cleanup, traversal/absolute/alternate-stream/noncanonical paths, duplicate/link entries, missing/changed/oversized metadata, source/database mismatches, omitted and extra references, and an unbalanced journal with an updated database file hash. Wrong recovery/ciphertext/recorded facts and failure/cancellation at decryption/extraction/database stages preserve active data. All 576 .NET cases pass, and the Windows Debug build has no warnings/errors; the four Go groups remain passing. Restore activation/instance protection, desktop controls, scheduling, cloud delivery, and actual second-device acceptance follow separately. See [Review a staged archive restore](archive-restore.md).

## Confirmed public recovery settings and local job history

Portable recovery settings now verify the saved protected material and passphrase, then retain only the public recipient, fingerprints/file hash, owner, and time. Identical confirmation is idempotent. Manual archive jobs persist requested and actually captured watermarks, completed ciphertext/manifest facts, fixed failure codes, and separate prior success. Their operational history does not advance the business watermark. Later changes remain Pending. Authorized completion can record after the human session locks without renewing it. An unconfirmed persisted Running attempt is Needs review when no matching job is active in this process; published ciphertext with failed history is explicitly reported for file review.

Seven new cases cover secret-free/idempotent configuration, wrong proof/foreign access, restart persistence, no watermark loop, later changes, locked-session completion, failure/cancellation, and interrupted history after publication. All 583 .NET cases pass, and the Windows Debug build has no warnings/errors; the four Go groups remain passing. Desktop controls, scheduling/restart resolution, cloud outputs, activation/instance protection, and actual device acceptance follow separately. See [Keep local archive history](local-backup-history.md).

## Desktop local backups and recovery-file setup

The Backups and recovery workspace now saves a new protected recovery file through the native Windows picker, rereads and verifies it before confirming public settings, accepts existing protected material, creates/cancels local encrypted archives, and reviews paged persistent job/manifests and encrypted-file checksums. Setup clears passphrase fields after attempts/unload. Existing outputs are preserved. The dashboard reads actual local coverage separately from its financial capture; Drive, Sheets, and second-device restore retain separate uncompleted states.

One new saved-material case checks exact rereading/helper inspection, destination preservation, and input bounds. All 584 .NET cases pass, the four Go groups remain passing, and Windows Debug has no warnings/errors. Fictional native checks saved/confirmed protected material, verified blank fields, created/checksummed a full archive, and retained configuration/history/dashboard coverage after restart. Wrong proof and changed ciphertext were rejected. Even after the native overwrite prompt, the app preserved the earlier archive byte-for-byte, recorded a failed attempt, and retained earlier successful verification. The initial automation probe used an Open-dialog filename ID; inspecting the Save dialog identified its different ID, after which the native check passed. These development-computer checks do not establish device acceptance. Scheduling, Google delivery, staged review UI, activation/instance protection, and real second-device tests follow separately. See [Create an archive from the desktop](desktop-local-backups.md).

## Desktop staged restore review

Archive review is now available before owner sign-in and after a storage-open failure, with independent native archive/recovery-file selection. Completed backup history also opens review bound to its recorded ciphertext and manifest. The interface shows verified identity/mode/schema, captured watermark/time, hashes, journal facts, and paged file/table counts. It clears passphrases and disposes staged plaintext on close/navigation, supports cancellation, and rejects a late result after the view has unloaded. It does not switch the working store.

The latest full suite remains 584 passing .NET cases with four passing Go groups. Windows Debug has no warnings/errors. Fictional native checks rejected wrong recovery, validated the recorded archive's two movements/five journal lines and all three managed versions, and removed the workspace on close. Standalone selection validated before sign-in, explicitly labeled absent local history, and cleaned up on returning to sign-in. Native cancellation stopped review and cleaned its generated workspace. The first script clicked validation before the picker continuation had stored its selection; waiting for the selected-file label corrected that probe. Restore activation/instance protection, scheduling, cloud delivery, and actual device acceptance follow separately. See [Review a staged archive restore](archive-restore.md).

## Writable-instance and restored-session guards

A failing-before test showed that a copied/reidentified database with the same logical Store ID accepted the original owner session. Schema 32 now persists a separate writable-instance identity/state, binds owner sessions to it, and rechecks active/current identity on connection open and inside writer/snapshot reservations. This includes internal ownerless transactions and credential proofs that finish after an instance change. Missing/invalid current metadata is preserved rather than regenerated. Older apps reject schema 32 on open. Archive capture checks its snapshot instance; restore review accepts supported complete schema-31 archives without migrating them.

Eleven new cases cover copied-session rejection, stale handles/ownerless writes, retirement after opening but before the writer reservation, changed generation during proof, persisted retirement, malformed/missing metadata, preserved version-31 upgrade snapshots, stable reopening, and older complete archive review. All 595 .NET cases pass, the four Go groups remain passing, and Windows Debug has no warnings/errors. Owner-confirmed retirement, prepared restore activation, cloud work, scheduling, and actual device acceptance follow separately. See [Keep writable store instances separate](store-instances.md).

## Prepared recovered TEST working copies

The recovery service now rereads the hashed original manifest, independently copies/rechecks staged files and database facts, migrates the separate copy, assigns a fresh writable-instance identity, and reestablishes the selected active owner's passphrase without changing other owners' credentials or historical approvals. It records a distinct recovery audit/provenance, actual source hashes/watermark, known source instance, new instance, owner/reason, and the caller's previous-primary acknowledgement. It publishes only to a new generated request directory while the review is still open. It does not switch the app's active store or claim to retire another computer.

Eighteen new fictional cases cover original financial JSON/files/balances, selected/other owner access, fresh-instance rejection, secret-free provenance, independent later postings, repeated/concurrent destinations, changed staged bytes/manifest, mutable display facts, disposed reviews, decision requirements, staged failure/cancellation, and schema-31-to-32 preparation without modifying the reviewed database. All 613 .NET cases pass, the four Go groups remain passing, and Windows Debug has no warnings/errors. Desktop preparation, retirement/active-store switching, scheduling, cloud delivery, and actual second-device acceptance follow separately. See [Prepare a recovered working copy](prepare-recovered-store.md).

## Desktop recovered-copy preparation

Validated TEST archive review now offers explicit owner selection, reason, repeated new word passphrase, and prior-primary acknowledgement before preparing one separate working copy. It reports the prepared folder and recovery provenance, supports cancellation, clears secret fields, and preserves the completed copy when temporary review closes. It does not select that copy as the application's active store.

The latest full suite remains 613 passing .NET cases with four passing Go groups; Windows Debug has no warnings/errors. A native fictional run blocked the absent acknowledgement, prepared one schema-32 copy from the earlier archive, and retained it after review close. Read-only comparisons verified identical financial/document/managed-version source rows, two movements/five journal lines, three versions, stable logical identity, a fresh instance, selected-owner-only credential change, one copied recovery audit watermark, and unchanged original watermark. This simulated acknowledgement does not establish real-device retirement. Active-store selection/retirement, scheduling, Google delivery, and device acceptance follow separately. See [Prepare a recovered working copy](prepare-recovered-store.md).

## Owner-confirmed local instance retirement

New archive jobs now retain their source writable-instance identity while older absent values remain unconfirmed. Retirement requires current owner proof, a reason, and an independently size/hash-verified complete archive covering this exact instance and current business watermark. The writer transaction rechecks coverage/history/access before recording Retired state, audit, provenance, and one final watermark. It preserves the database/archive and locks the caller only after commit. It does not remotely retire another computer or switch active stores.

Ten new cases cover confirmed retirement with unchanged archive/history, missing/changed/old/unconfirmed-instance archives, wrong proof/foreign access/reason, later business changes during verification, and interrupted/cancelled transaction rollback. All 623 .NET cases pass, the four Go groups remain passing, and Windows Debug has no warnings/errors. Desktop retirement/active-store selection, scheduling, cloud delivery, and actual device acceptance follow separately. See [Retire this local writable instance](retire-local-instance.md).

## Controlled recovered-store activation and persistent selection

New recovered copies now remain Prepared and reject normal business-store access until activation. Version-2 recovery provenance retains the source manifest for final checks. Fresh recovered-owner proof keeps normal persisted lockout; activation revalidates identity/watermark, table/financial facts, retained source hashes, managed references, and actual bytes under a writer reservation. An accessible previous local primary of the same logical business must already be retired. Activation records its own audit and a relative, identity-bound persistent selection. Unsafe/malformed/mismatched selections do not silently open another store.

Twenty new cases cover local retirement/new-root activation, later postings/repeated sign-in, changed files/data/counts/provenance, rollback of absent/existing pointers, committed-result errors, path/identity boundaries, simulated interrupted-state completion, external changes, lockout, concurrent readers, and cancelled fresh proof. The reader probe failed on Windows sharing errors/transient absence; changing share flags alone failed too. A short filesystem publication gate made forty replacements readable as complete selections. One extra-record fixture query was corrected to the actual customer schema. All 643 .NET cases pass, the four Go groups remain passing, and Windows Debug has no warnings/errors. Earlier prepared copies are preserved and require fresh preparation for this flow. Desktop retirement/selection, scheduling, cloud delivery, and actual device/power-loss acceptance follow separately. See [Select a recovered TEST working store](active-test-store.md).

## Desktop retirement, activation, and restart

Sign-in and recovery screens now discover generated prepared copies, show their business/owner/status/source facts, and activate only after fresh proof and final validation. Startup and owner switching resolve the saved identity-bound selection. Completed archive history offers explicit local retirement with current-owner confirmation, archive/coverage verification, cancellation, and an unsaved-fields decision. An intentional retirement now shows a recovery screen rather than keeping an unusable workspace. Long operations run off the UI thread and secret fields clear on completion/unload.

All 643 .NET cases pass, the four Go groups remain passing, and Windows Debug has no warnings/errors. A native run in a separate fictional fixture prepared an archive/copy, blocked activation while its old local primary was writable, preserved a cancelled retirement confirmation, retired that fixture, rejected wrong recovered-owner proof, activated, restarted, verified pinned receipt v1, and retained January $0 income/$10 expense/-$10 result and bank. Read-only comparisons confirmed exact financial/document/file/link sources, two movements/five balanced journal lines, three managed versions/hashes, and expected audit-only watermark differences. The earlier rehearsal store remained unchanged. The script initially entered a secret while the cancelled dialog was still reenabling fields; waiting for enabled state corrected the probe. This is development-computer evidence, not Surface, physical power-loss, or second-device acceptance. Scheduling, Google delivery, LIVE setup, release packaging, and remaining acceptance work continue. See [Select a recovered TEST working store](active-test-store.md).

## Instance-specific permission for automatic local archives

An active owner can now save a revisioned, audited interval and private local folder for this writable instance after confirming portable recovery material. The internal archive permit contains the public recipient and exact saved consent, with no human session or private key. Capture rechecks consent against the copied database; publication rechecks the current policy. Changed settings, recipients, confirming-owner access, or instances invalidate earlier permission. Restored instances do not inherit another instance's enabled schedule.

Nine new fictional cases passed alongside the existing archive/source cases (30 targeted cases). They verify actual encrypted output with a locked human session left unchanged, revocation after encryption, destination restrictions, copied-instance rejection, and idempotent settings. All 652 .NET cases pass; Windows Debug has no warnings/errors. The four Go groups remain passing. No recurring worker or schedule controls run yet; persistent automatic job history and a coalesced queue follow in focused changes. See [Authorize automatic local archives](automatic-archive-permission.md).

## Persistent automatic archive attempt history

Consent-bound automatic archives now use the existing durable job history with generated nonoverwriting destinations, schedule identity/revision, current instance, requested/captured watermarks, fixed failure codes, and verified output facts. They preserve locked human sessions and do not advance the business watermark. Completion rejects changes to any immutable attempt fact. Current health counts only the current writable instance; copied/unknown-instance history remains available without asserting current execution or coverage.

Seven new cases and all seven existing local-history cases pass (14 targeted cases). They cover actual automatic encryption/reopening, locked-session preservation, later pending changes, failure/cancellation, revoked consent, historical-instance separation, and changed-attempt rejection. The latest complete suite remains 652 passing cases; the recurring queue/worker and schedule UI follow separately. See [Authorize automatic local archives](automatic-archive-permission.md).

## Persistent coalesced automatic archive queue

The queue engine now observes committed business watermarks, coalesces pending changes, retains interval/retry state across restart, and runs one automatic attempt under an exclusive file lease. Actual captured coverage remains separate from later changes. It reuses completed coverage from this instance/recipient without a watermark loop, retries failures at bounded exponential intervals, and leaves disabled/restored instances unauthorized. Earlier uncertain automatic Running records become Interrupted/CompletionUnconfirmed without deleting files or claiming verification.

All 667 .NET cases pass and Windows Debug has no warnings/errors. Tests cover restart/interval/coalescing, later changes, unchanged locked access, persisted backoff, duplicate-worker exclusion, cancellation/lease release, unconfirmed publication, and changed policy/instance. The interval probe initially checked several seconds before its completion-based due time; the test was corrected to advance beyond that interval. The desktop recurring loop and owner schedule controls are still pending; this engine alone does not start a schedule. See [Keep automatic archive work pending](automatic-archive-queue.md).

## Archive capacity estimates

The capacity service now estimates a consistent logical database plus all managed file versions without creating a snapshot/output. It budgets copied capture, package, encrypted output, bounded metadata, and encoding overhead against observed drive-root capacity, combining same-root work. Unknown values stay unknown. Automatic work stops on a known shortage before capture and records InsufficientLocalSpace; later write failures remain possible because estimates reserve no space.

Three new capacity cases pass alongside all eight queue cases (11 targeted cases). The source estimate exactly matches the subsequent verified capture, retains replaced versions, leaves watermarks unchanged, creates no staging directory, distinguishes unknown/short/sufficient space, and rejects missing/foreign sources. The latest complete suite is 667 passing cases from the preceding queue change. Desktop schedule controls and the recurring app loop follow separately. See [Estimate local archive space](archive-space-estimate.md).

## Desktop recurring local archives

Owners can now explicitly choose a native private folder, review capacity, save a revisioned interval/enabled choice, and inspect queue/runtime/history states. Unsaved schedule fields survive navigation. A cancellable app-lifetime loop checks saved consent/pending work every fifteen seconds, including before sign-in, without touching human sessions. Normal window close waits for current and previously stopping loops. Selected-instance changes and retirement stop the earlier runtime. Historical job details show source instance and automatic schedule/revision. Google and second-device success remain separate.

All 670 .NET cases pass and Windows Debug has no warnings/errors; the four Go groups remain passing. A separate fictional native fixture selected a folder, estimated capacity, enabled scheduling, signed out, produced exactly one verified archive, closed/restarted, retained settings/history, and disabled without another output. Read-only checks confirmed exact financial/document/evidence/link rows and three managed hashes, two movements/five balanced lines, archive watermark 33, two schedule-only audit increments to 34, no staging leftovers, and the untouched earlier store at 32. Actual Surface, forced termination/power loss, second-device recovery, cloud, LIVE setup, retention, and release acceptance remain pending. See [Schedule local encrypted archives](desktop-automatic-backups.md).

## Final archive publication permission race

A failing-before regression revoked schedule consent after the previous publication check but before final ciphertext verification, and the old code still published. Automatic publication now performs its final current-policy/recipient check and nonoverwriting rename inside one short writer reservation, after all checksum/encryption work. Revocation and publication therefore have a defined order without holding the writer during long work.

The new regression and all related encryption/archive/restore/schedule/queue cases pass (63 targeted cases). Windows Debug has no warnings/errors. The latest complete suite remains 670 passing cases before this one addition. Google delivery and the remaining full-build work continue.

## Windows-profile protected Google credential files

The internal credential-file component now uses pinned Microsoft ProtectedData/Windows DPAPI CurrentUser protection with business/store/instance/mode/purpose binding. Files live outside the business directory and complete archive allowlist. Writes flush new ciphertext, revalidate current context under a short writer reservation, and publish under an exclusive file gate; uncertain/corrupt context files are preserved with fixed reconnect errors. Caller-owned plaintext remains in memory only. OAuth and connection controls follow separately; no real Google authorization or connector-token reuse occurred.

All 678 .NET cases pass, including seven focused credential cases: actual protected reopening, capture exclusion, instance/business/purpose/ciphertext rejection, failure during replacement, changed identity after flushing, bounds/private path separation, purpose-specific deletion, and twelve concurrent replacements/readers. Windows Debug has no warnings/errors, and package notices/license are present in test/desktop output. Separate-profile/second-device acceptance and OAuth/delivery remain pending. See [Protect Google device credentials](google-device-credentials.md).

## Google Desktop OAuth client validation

A bounded, side-effect-free parser now accepts the app's own downloaded Desktop/installed OAuth configuration and requires supported official Google endpoints. It rejects Web/service-account configuration, duplicate/missing fields, untrusted endpoints, malformed/invalid UTF-8 input, and oversized files. Internal display and error messages omit private configuration. It does not import connected-app tokens or perform authorization; PKCE/state/browser consent and connection controls follow separately.

All eleven new parser cases pass. The latest completed full suite has 678 passing cases, including credential protection and the final archive-publication regression; the last Windows Debug build has no warnings/errors. See [Validate the app's Google desktop client](google-desktop-client.md).

## Google PKCE and state-checked browser consent protocol

The internal protocol now uses pinned Google.Apis.Auth 1.78.0 S256 PKCE, narrow drive.file scope, offline consent/account selection, one-use random state, exact 127.0.0.1 callback binding, and a five-minute cancellable deadline. The system browser uses Windows URL handling without a command shell. There is no disk token store in this protocol. Complete offline material and returned scope are checked; fixed errors omit provider/private content. Google SDK logging remains its default NullLogger, with no debug logging configured.

All 700 .NET cases pass, including eleven protocol and eleven parser cases with an independent verifier/challenge comparison and the actual SDK loopback receiver with a fictional callback and no Google browser/account. Windows Debug has no warnings/errors; pinned/transitive license notices are present in output. Owner-authorized configuration/grant storage, UI controls, real consent, and cloud outputs follow separately. See [Authorize a Google desktop connection](google-oauth-protocol.md).

## Owner-authorized Google desktop configuration

The configuration service now validates and protects the app's own downloaded Desktop client outside the business directory, with nonsecret generation/revision/project/fingerprint/owner/time metadata and one audit per actual change. Identical client facts are idempotent. Final publication rechecks active owner access and the reviewed configuration identity. A file/database mismatch after an interrupted commit fails closed; explicit owner-reviewed replacement can repair damaged/mismatched protected files. Secret byte buffers owned by the service are cleared after use.

Eight new settings cases and all seven credential cases pass (15 targeted cases), including reopening, idempotent audits, archive exclusion, foreign/locked/stale access, owner lock after flushing, rollback, interrupted generation mismatch/repair, concurrent replacement, and restored-instance isolation. Windows Debug has no warnings/errors. Current NuGet sources report no vulnerable Business dependencies. The latest full regression suite remains 700 passing cases; saved grants, connection UI, actual consent/account checks, and delivery follow separately. See [Save the app's Google desktop configuration](google-client-settings.md).

## Bounded Google account and storage verification

The account probe now requests only authenticated Drive identity/email/display name and storage limit/usage from a pinned endpoint, with bearer material only in its header. The default client disables redirects/cookies. A bounded JSON reader handles declared and streamed response limits, fixed private-safe grant/quota/storage/access/server errors, explicit cancellation, and a complete sixty-second request deadline. Missing limits stay unknown and over-limit usage yields zero observed capacity. No real Google request or upload was made.

All 726 .NET cases pass; Windows Debug has no warnings/errors. A stalled-body probe first showed the header timeout did not bound response reading; an operation deadline corrected it. Tests also cover malformed/overflowed facts, absent limits, declared/streamed bounds, invalid bearer values, I/O privacy, status reasons, and cancellation. Verified grant persistence, desktop controls, real account acceptance, and delivery follow separately. See [Verify the authorized Google account](google-account-verification.md).

## Owner-authorized verified Google grants

The connection service now performs consent and account verification outside writer reservations, snapshots validated narrow offline material, and rechecks the reviewed client/connection generations, owner access, and cancellation at protected grant publication. Public provenance/account/storage facts and one connection audit remain separate from encrypted device tokens. Background loading checks current consent/client/file bindings without renewing human access. Unconfirmed cross-resource publication fails closed and can be repaired with explicit fresh consent; unavailable files need review rather than claiming local readiness.

Ten new grant cases plus eight settings cases pass (18 targeted cases), including protected reopening, actual archive exclusion, locked-session preservation, foreign/unconfigured access, owner lock/client changes during consent, verification failure/cancellation, interrupted publication/repair, concurrent completion, unavailable-file recovery, and changed/restored/deactivated contexts. The last Windows Debug build has no warnings/errors. The latest complete suite is 726 passing cases before these ten additions. Local disconnect/refresh, desktop controls, real Google consent/account acceptance, and delivery follow separately. See [Save a verified Google connection](google-connection-grants.md).

## Local Google disconnection and guarded token cleanup

An owner-reviewed local disconnect now records a fresh authority generation, actor/time, prior connection identity, and one audit while preserving last verified account facts and client settings. Earlier in-flight consent cannot undo it. Token cleanup separately rechecks the disconnected generation before deletion, so delayed cleanup preserves a newer explicit connection. Incomplete cleanup is reported separately and never restores local authority. This operation makes no remote request or server-revocation claim.

All 741 .NET cases pass and Windows Debug has no warnings/errors. Five new disconnect cases cover audit/idempotence/reopen/reconnect, foreign/locked/stale access, unavailable cleanup/retry, in-flight consent invalidation, and replacement-grant preservation; the 22 related cases also pass. Token refresh, explicit remote revocation, desktop controls, actual Google acceptance, and delivery continue separately. See [Disconnect Google from this local instance](google-local-disconnect.md).

## Google access-token renewal without human-session renewal

Near-expiry access now renews through the pinned SDK token endpoint without a browser or disk token store. An exclusive private-file lease coalesces concurrent renewals; readers reload after acquiring it. Cached unexpired access avoids network work. Renewal retains omitted refresh material, validates narrow scope, verifies the same Google account identity, and checks current saved consent at protected publication. Its default transport disables redirects/cookies. Network/overall deadlines and explicit cancellation bound work. Token maintenance leaves public connection provenance, business watermark, and locked human sessions unchanged.

All 752 .NET cases pass and Windows Debug has no warnings/errors. Eleven new refresh cases cover cache/locked access, protected renewal/no audit loop, omitted refresh material, three concurrent callers, configuration/disconnect changes, another account, cancellation/lease release, actual SDK POST construction, and fixed invalid/broad-response errors. All 27 related refresh/consent/disconnect cases pass. Persistent reconnect state, remote revocation, desktop controls, real Google consent/account acceptance, and cloud outputs continue separately. See [Renew Google access without unlocking an owner](google-token-refresh.md).

## Persistent Google reconnect requirements

Recognized grant/client/material/scope/account rejection now persists a fixed, generation-bound issue with no provider text or credentials. Loading stops for that generation across restart, rather than repeatedly renewing a rejected grant. Temporary network/quota/time failures retain consent. Successful explicit consent clears the older issue atomically with its new connection record. Delayed older failures cannot affect replacement clients/connections, and local disconnect remains available. Operational issue state changes no business watermark or human-session activity.

Six new cases plus renewal/disconnect cases pass (22 targeted cases); Windows Debug has no warnings/errors. They verify rejection/restart/no retry loop, unchanged locked access, fresh consent, offline recovery, stale-failure isolation, unknown-detail exclusion, and disconnect/configuration boundaries. The older account-mismatch test now inspects preserved bytes directly because the grant is correctly unavailable for use. The latest full suite remains 752 passing cases before the six additions. Desktop connection controls, actual Google acceptance, and cloud outputs continue separately. See [Require fresh consent after a rejected Google grant](google-reconnect-state.md).

## Await registered cancellable work during desktop close

A small application operation coordinator now registers cancellable work before starting it, combines caller/application cancellation, waits for action cleanup, and rejects new work once shutdown begins. Ordinary operation failure still reaches its caller and does not leave shutdown waiting. Desktop close awaits this coordinator alongside local backup runtime shutdown. Feature views must explicitly register their work; the upcoming Google controls will do so. This does not yet register every existing editor or resolve unsaved-field decisions.

Three coordinator cases and six reconnect cases pass (9 targeted cases); Windows Debug has no warnings/errors. They verify cancellation plus awaited cleanup, refused late starts, ordinary failure, and isolation between caller cancellations. The latest complete suite remains 752 passing cases before these and the reconnect additions. Google connection controls and remaining full-build work continue.

## Desktop Google connection controls

Owners can now import the app's own Desktop client through a bounded native JSON picker, start PKCE browser consent, review saved account/storage observations, cancel pending work, disconnect locally, and open Google's account access management. Client secrets/passwords/tokens have no app display fields. Registered import/consent/disconnect work participates in coordinated close; leaving the view cancels its pending operation. The screen keeps connection, Drive delivery, Sheets export, and restore-test states separate.

All 761 .NET cases pass and Windows Debug has no warnings/errors. A fictional native restart/reimport retained revision 1 and Not connected. Read-only checks confirmed exactly one configuration audit/watermark increment, protected configuration outside the business directory, no Google token/connection, unchanged financial/document/evidence/link rows and all three file hashes, balanced journal totals, and the earlier fixture unchanged. No real Google consent/request, Surface, or second-device acceptance occurred. Account settings provide Google's server-grant management; local disconnect makes no server-revocation claim. Cloud outputs and the remaining full-build work continue. See [Connect the app to Google](desktop-google-connection.md).

## Private Google output-folder verification

The internal Drive component now creates only an empty, generic, app-bound My Drive folder with domain-default visibility bypassed and no sharing request. An independent read requires the connected account as its sole owner/permission, matching app binding/type, writable capability, no shared-drive identity, and explicit unshared/untrashed facts. It sends no business data and cannot enable delivery by itself.

Eleven fictional HTTP cases pass. They verify request shape and independent post-create checking; reject shared/group/domain/foreign-owner/shared-drive/trashed/binding/read-only/type changes; and reject missing facts, path injection, and malformed bearer input. Group/domain cases retain shared=false to challenge reliance on that flag alone. The latest complete suite remains 761 before these eleven additions. Owner destination consent, delivery, actual Google account acceptance, and the remaining full-build work continue. See [Verify a private Google output folder](google-private-folder.md).

## Owner permission for separate Google outputs

Verified destination creation now saves audited instance/connection-bound settings with both outputs disabled. Active owners enable Drive archives and Sheets export independently after another protected-connection/private-folder check. Final saves recheck access, consent, exact reviewed policy, and cancellation. Identical choices are idempotent. Offline local withdrawal needs no usable Google grant; new folders reset both choices. Background permits recheck exact policy/connection/confirming-owner authority without touching human sessions or business watermarks.

Seven new policy cases plus eleven folder cases pass (18 targeted cases), including concurrent folder replacement during verification, late owner lock/cancellation, offline withdrawal after disconnect, generation/instance isolation, and fixed persisted hard grant rejection. The first follow-up compile exposed an incorrect overload argument in rejection observation; passing the access grant corrected it. The latest complete suite remains 761 before these eighteen additions. Desktop output controls, delivery, actual Google acceptance, and remaining full-build work continue. See [Authorize each Google output](google-output-permission.md).

## Resumable encrypted archive transfer

The internal uploader now checks complete local size/hash and the age header before network work, generates Drive identities for durable callers, initiates a new app/job/hash-bound binary file in the confirmed parent, and sends bounded four-MiB chunks. It accepts only the pinned HTTPS Google upload location, keeps that private session address in memory/redacted display, reacquires access before each request, advances from actual acknowledgments, and queries status after uncertain responses with bounded recovery/backoff. Exact file ID, length, and Drive SHA-256 are required for a confirmed transport result.

Eighteen new fictional cases and account/folder cases pass (47 targeted cases); Windows Debug has no warnings/errors. Partial acknowledgment resumes at the returned offset; lost final response confirms without another content write. Malicious locations, invalid progress, changed/missing completion facts, changed/plaintext sources, and cancellation fail without false success. The first run found a one-byte-short header allocation; using the format's own byte length fixed it. Durable history, final remote privacy/authority checks, desktop delivery, real Google acceptance, and remaining full-build work continue. See [Send an encrypted archive in resumable chunks](google-archive-upload.md).

The complete regression suite now passes all 797 .NET cases, including the eighteen folder/policy additions and eighteen upload cases. The four recovery-crypto groups remain passing; no Go source changed in these slices.

## Verify remote archive completion and privacy

Remote archive reads now require exact identity, length, checksum, parent, app/job/hash/output bindings, downloadable binary type, and private sole ownership/access. Folder and file checks share one small ownership validator. An earlier uncertain completion can be verified without content writes. Unavailable-file 404 and existing-identity 409 remain distinct from access/network failures; unavailable does not prove global absence. No existing remote file is overwritten or deleted.

Thirteen new cases plus related transport cases pass (60 targeted cases), including group access despite shared=false, changed bindings/content/location, missing facts, and distinct unavailable/access/conflict states. The latest complete suite remains 797 before these thirteen additions. Durable attempts, desktop delivery, actual Google acceptance, and remaining full-build work continue. See [Verify a completed Google archive](google-archive-verification.md).

## Durable Google archive delivery and attempt records

Deliveries now reserve one Drive identity for exact completed source/account/folder/recovery facts before content transfer. Repeated/concurrent reservations coalesce. Each attempt retains its own policy revision, status, times, and fixed failure code; earlier failures and interrupted outcomes remain visible. Completion checks exact current authority, immutable source/attempt/result facts, and current recovery material. Withdrawal blocks success while permitting failure history; later explicit permission for the same destination can reuse the original Drive ID. Operational history changes no business watermark or human-session activity.

Seven cases pass with actual local archive creation, covering locked background completion/reopen, retained failure/interruption/retry, immutable attempt rejection, withdrawal/regrant, changed source path/instance/recipient, and concurrent reservations/running-attempt exclusion. No real Google request occurred. The latest complete suite remains 797 before these and the thirteen remote-verification additions. The worker must hold an exclusive lease before declaring earlier Running attempts interrupted. Queue/worker, desktop controls, actual Google acceptance, and remaining full-build work continue. See [Retain Google archive delivery attempts](google-archive-delivery-history.md).

## Deliver saved archives through the complete transfer and verification path

The worker now holds an exclusive store lease, verifies the private destination, reserves/reuses a durable file identity, retains separate attempts, checks earlier remote completion, transfers pinned/hashed ciphertext if needed, rechecks authority/source/recovery before every request, independently verifies remote content/bindings/privacy and the folder, and records success only after a final short authority transaction. Cancelled/withdrawn/uncertain outcomes retain remote files and useful local history. Hard grant rejection persists reconnect state. Earlier uncertain attempts are interrupted only under the worker lease.

Seven worker and seven history cases pass (14 targeted cases); Windows Debug has no warnings/errors. The integrated case transfers real encrypted bookkeeping/PDF/evidence bytes through the actual transport against a simulated HTTP service, then decrypts/stages the received copy with matching facts and watermark, while preserving locked access and the current watermark. Other cases cover retry without reupload, late withdrawal/later changes, local corruption before content requests, changed remote group access, lease exclusion/interruption, and late cancellation. No real Google request occurred. Queue/runtime, desktop delivery controls, actual account/device acceptance, and remaining full-build work continue. See [Deliver a saved encrypted archive](google-archive-delivery-worker.md).

The complete .NET suite passes all 824 cases before one added cancellation-boundary regression. That regression failed before the fix: cancellation after the earlier check but before the writer reservation still recorded success. The worker now passes cancellation into completion, which rechecks inside its final writer transaction. Eight worker and seven history cases cover the corrected boundary (15 targeted cases). Queue/runtime and desktop delivery controls continue separately; the four crypto groups remain passing with unchanged Go source.

## Google archive health and exact current coverage

Health now separates current local watermark, exact last verified scope-specific archive coverage/time, latest attempt, and retained last success. Failures, disabled choices, and disconnect preserve earlier successful coverage. New folders/instances start separate current coverage while older delivery history remains visible. Running requires this process's active worker registration; uncertain saved Running attempts need review. Background review touches no human session or business watermark and makes no new remote/restore claim.

Five new cases pass and Windows Debug has no warnings/errors. They cover later pending changes, unknown Running history, locked access, failure/disable retention, new-folder/copied-instance isolation, and actual active-worker completion/disconnect against the simulated service. The latest complete suite remains 824 before the cancellation regression and these five additions. Queue/runtime, desktop controls, real Google/device acceptance, and remaining full-build work continue. See [Review Google archive coverage](google-archive-health.md).

The complete .NET suite now passes all 830 cases, including the final writer-cancellation regression and five health cases.

## Persistent delivery queue for completed local archives

The queue engine now selects the newest eligible completed local archive for current instance/recovery material, coalesces verified coverage without network requests, and retains observed/delivered watermarks. Offline/quota failures keep fixed error/backoff state across restart, bounded from one to sixty minutes. A separate lease serializes queue decisions while the worker retains its delivery lease. Changed policy/account/folder scope resets retry state. Disabled permission starts no request; later changes wait honestly for a newer local archive. This engine does not create archives or start a timer by itself.

Five new cases pass and Windows Debug has no warnings/errors. They cover signed-out restart/no repeated calls or session/watermark renewal, persisted offline/quota delays, later pending changes without older reupload, lease exclusion, copied-source rejection, and withdrawn permission. The latest complete suite is 830 before these five additions. Desktop polling/controls, Google archive creation integration, Sheets export, real account/device acceptance, and remaining full-build work continue. See [Keep saved-archive delivery pending](google-archive-queue.md).

The complete .NET suite now passes all 835 cases, including the five queue cases.

## Offline withdrawal of either Google output

Two failing-before regressions showed that disabling only Drive or only Sheets while preserving the other saved choice attempted remote verification. Pure permission reductions now save locally without a usable Google request; adding permission or re-saving unchanged enabled choices still verifies the connection/folder. Existing owner, reviewed-policy, final cancellation, and consent checks remain in place for their applicable operations. All nine policy cases pass. Desktop controls and remaining full-build work continue.

## Desktop Google archive controls and recurring delivery

Owners can now create/review a verified private folder, explicitly save the Drive choice, choose a completed local archive for immediate delivery, review exact scoped health/retry/history/attempts, cancel foreground work, and reload saved fields without losing unsaved choices during navigation. Registered foreground work participates in coordinated close. A separate app-lifetime loop checks completed local archives every fifteen seconds, including before sign-in, and stops/awaits cleanup on close, selected-instance change, or retirement. Slow Google work uses a separate loop from local archive scheduling. Sheets and restore checks remain separate; a stale workspace banner no longer claims cloud is disconnected.

All 837 .NET cases pass and Windows Debug has no warnings/errors. A native fictional rehearsal verified default-off/unconfigured controls; missing-account/folder/source/history guards; unsaved checkbox navigation/reload; signed-out close; and restart with both loops. Read-only checks retained financial/document/evidence/link rows, all three hashes, balanced journal facts, the earlier configuration-only watermark/audit, no Google token/connection/destination/delivery, and the original fixture unchanged. No real Google request occurred. Configure local scheduling or create a local archive to cover new changes; the Drive queue honestly waits when none is ready. Readable Sheets export, real Google/device/recovery acceptance, LIVE setup, access/workspace hardening, packaging, and remaining full-build work continue. See [Deliver encrypted archives to Google Drive](desktop-google-archives.md).

## Consistent read-only source for readable exports

Readable export capture now makes a private standalone SQLite snapshot, validates identity/schema/integrity/relationships/retained hashes and exact journal facts, and records a generation/mode/watermark/time. Manual capture requires an owner; background capture requires exact saved Sheets permission against copied and current authority, with no human-session renewal. Drive-only consent is insufficient. Later local changes stay outside the source. The private database itself is not a readable payload; explicit allowlisted projections follow separately.

Seven new cases cover WAL consistency with later local writes, read-only enforcement, locked-session/watermark preservation, late withdrawal/cancellation/deactivation/instance change, wrong output permission, disposed access, and owned-file cleanup that preserves unexpected files. The latest full suite is 837 before these additions. No Google request occurred. Projections/reports, Sheets transport/generation publication/UI, real account/device acceptance, and the remaining full-build work continue. See [Capture one readable-export source](readable-export-source.md).

## Explicit business/customer readable tables

The first four source-bound tables now expose only selected business facts, owner names/status, customer contact/billing/provenance, and photo-status history. Private notes/preferences/sensitivities/scopes, owner authentication/lockout, media, and future unselected fields stay out. A bounded scalar-only builder retains exact money/decimal/date/ID text, leaves unknown values blank, freezes rows/schema, and computes stable generation/watermark-bound table hashes. It rejects oversized or unsupported content without truncation. This is a partial projection; no complete workbook is published.

Nine table cases plus seven source cases pass (16 targeted cases); Windows Debug has no warnings/errors. Private sentinel/future-field checks, exact extreme money values under French culture, formula-like names, immutability, budgets, rejected complex/binary/double values, stable ordering/hashes, and later-source isolation pass. The initial compile needed an explicit generic collection type; tests were corrected for the fixture's seeded customer/history. The latest complete suite remains 837 before these sixteen additions. Other groups/reports, RAW Sheets transport/readback, verified generation publication/UI, real account/device acceptance, and remaining full-build work continue. See [Project readable business and customer tables](readable-contact-tables.md).

The complete .NET suite now passes all 853 cases, including source capture and the first table group.

## Explicit catalog and booking readable tables

The next five source-bound tables retain current catalog definitions, service revisions, booking details/provenance, copied charges, and named/unnamed/inactive places. Existing booking rules supply exact net/tax/total and payment-plan facts without inventing unknown tax, duration, dates, or payer identities. Original copied prices and historical service revisions remain distinct from current catalog prices. Private booking notes and free-form tax evidence stay out; retained record identity/revision must match relational facts.

Three new cases pass: current/retained prices, fractional travel, named/unnamed/inactive places, private-field exclusion, unresolved versus zero tax, exact plan totals/unknown dates, and changed JSON identity rejection. The latest complete suite is 853 before these additions. Appointment/status/reservation projections, other domain groups/reports, Sheets transport/publication/UI, real account/device acceptance, and remaining full-build work continue. See [Project service and booking records](readable-planning-tables.md).

## Explicit schedule and reservation readable tables

Five more source-bound tables retain artists/provenance, appointments with parent booking state, explicit recipient places, ordered booking-status history, and exact reservation decisions. UTC instants and recorded offsets/time zones remain distinct; unknown times stay blank. Private artist/booking/appointment notes, products/shades, override explanations, and free-form status/reservation reasons stay in the desktop/archive. No scheduling or payment decision is inferred from the export.

Three new cases pass: actual ambiguous-hour offsets/cancelled-parent context, an actually approved/applied deposit and confirmed reservation with exact quote/amounts, and blank times with archived artist retention. Private sentinels stay out. The latest complete suite remains 853 before these and the three planning additions. Remaining domain groups/reports, RAW Sheets transport/readback, verified publication/UI, real account/device acceptance, and full-build work continue. See [Project appointments and reservation history](readable-schedule-tables.md).

## Explicit issued-document readable tables

Five more source-bound tables retain every issued document version, exact frozen party/event/terms/amount/hash facts, current recorded version status, issue/acceptance provenance, recorded PDF references, ordered lines/milestones, client-safe receipt details, and receipt application amounts. Current customer changes do not rewrite frozen parties; old accepted/superseded versions remain separate. Relational identity/date/mode must match the retained snapshot. Private customer/payment/evidence fields, acceptance-evidence text, raw JSON, binaries, and PDF error diagnostics stay out. Receipt balance fields remain historical observations.

Four cases pass: actual invoice/receipt/tax/application facts with later customer changes, credit-note linkage and rounding, amended accepted/superseded versions, empty schemas, and private sentinels. Fixture credit-draft field names and required receipt tax classification were corrected without changing business rules. Windows Debug has no warnings/errors. The latest complete suite remains 853 before the planning/schedule/document additions. Relational billing links, other domains/reports, RAW Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project issued document snapshots](readable-document-tables.md).

The complete .NET suite now passes all 863 cases, including planning, schedule, and issued-document projections.

## Explicit relational billing links

Seven additional source-bound tables retain invoice payer/quote links, exact billed portions/signatures, invoice milestones, credit-note/invoice links, credited line/milestone/tax reductions, and receipt-to-posted-payment identities. A shared reader uses explicit trusted SELECT lists with shape/scalar/budget validation, retains blank unknown dates and exact quantity/money text, and never emits generic JSON or changes business state.

Three new cases pass for actual invoice/partial-credit reductions, posted payment/receipt linkage without duplicate cash, and rejected wrong-shape/binary values. A Release build overlapped the prior full run and failed on its locked output; that full run completed with all 863 passing, and the sequential new-case rebuild passes. Financial/resource groups/reports, Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project billing and payment source links](readable-billing-links.md).

## Explicit financial draft, approved movement, and journal tables

Five more source-bound tables retain account/period settings, current financial draft status/provenance, original approved payload/provenance and exact money/reference/source facts, and journal debit/credit/account/asset identities. Approved payload identity/revision/date/kind/amounts must match relational facts. Incomplete draft dates/tax remain unknown. Evidence input text/files and cached internal generations stay out. Drafts, documents, applications, and gross movement amounts are not treated as additional cash or a profit report.

Three new cases pass for actual gross/tax/fee/expense facts and balanced journal sides, incomplete draft/evidence exclusion without posting, and linked reversal/opposite bank side. Projection preserves watermarks/movement counts. The latest complete suite remains 863 before billing-link/ledger additions. Applications/credits, resources/reports, RAW Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project drafts, approved movements, and journal lines](readable-ledger-tables.md).

## Explicit application and funded credit history

Five more source-bound tables retain customer credits, Reserve/Release funding and source/refund/reversal identities, application approval metadata, dated payment/credit applications and reversals, and separate credit drafts with blank unknown dates. Free-form reasons and cached generations stay out. These are movements of existing value; exporting them creates no new receipt/income and does not flatten positive/reversal rows into misleading net balances.

Three new cases pass for an actual receipt-funded credit/application/partial return with one cash receipt, dated application/unapplication with separate approvals, and an incomplete draft with no credit/funding. Private reasons stay out; Windows Debug has no warnings/errors. The latest complete suite remains 863 before billing-link/ledger/credit additions. Resource/banking/tax groups, reports, RAW Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project applications and funded credit history](readable-credit-tables.md).

The complete Release suite now passes all 872 cases, including billing-link, ledger, and credit projections.

## Explicit supplier, asset, and loan register tables

Four more source-bound tables retain supplier business contacts/terms, loan opening/lender/terms/due facts, asset registered/funding/disposal/provenance facts, and ordered registered-asset fact review history. Unknown dates/principal/cost/basis/use remain blank, while confirmed zero values remain zero. Supplier private notes and free-form review/evidence inputs stay out. Registered cost/tax basis are kept distinct from book carrying value; projection makes no posting or depreciation decision.

Two focused Debug cases pass for supplier privacy, unknown versus zero register facts, and an actual asset-fact review retaining previous unknown values without journal effects. The desktop Debug build has no warnings/errors. The latest complete Release suite is 872 before these additions. Operational/purchasing/banking/tax groups, reports, Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project suppliers, assets, and loan registers](readable-resource-tables.md).

## Explicit supply, trip, and reimbursement attribution tables

Six more source-bound tables retain supplies/status history, actual mileage/review history, and approved payment attribution/release identities with their frozen claim basis. Product/route/driver/cost/provenance facts remain distinct from customer-billed travel. Unknown dates/requested reimbursements stay blank; confirmed zero remains zero. Notes/evidence/payee/release explanations and raw snapshots stay out. These reads create no expense, cash movement, reimbursement, or inventory disposal.

Three operational cases plus the two register cases pass in Release (five targeted cases): supply dates/history/privacy, actual versus billed miles and unknown/zero claims, and actual attribution/release with frozen basis and one cash expense. The latest complete suite remains 872 before register/operational additions. Purchasing/banking/tax groups, reports, RAW Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project supplies and actual mileage history](readable-operational-tables.md).

## Explicit supplier bill, credit, and settlement tables

Five more source-bound tables retain supplier bills/lines/provenance, supplier-credit/reversal reductions, and posted settlement/funding allocations. Unknown tax/use keeps gross/business amounts unknown. Credit signs and settlement reversal/refund/consumption links remain explicit; total/business/personal-draw portions remain separate. Private credit reasons, evidence inputs, and cached generations stay out. Projection creates no expense or cash and does not equate registered asset value with book value.

Three new cases pass for actual incomplete/approved bills without posting, actual supplier credit/reversal and private-reason exclusion, and actual mixed business-cash funding with one cash movement. The latest complete suite remains 872 before register/operational/purchase additions. Banking/tax/evidence groups, reports, RAW Sheets transport/readback/publication/UI, real account/device acceptance, and full-build work continue. See [Project supplier bills, credits, and settlements](readable-purchase-tables.md).

## Bank source and match projections; full-suite scheduling investigation

Seven more tables retain statement balances/provenance, selected import mappings, original imported rows, bank transactions with active-source status, withdrawals, matching approvals, and dated portions/releases. Three fictional checks pass for actual corrected/withdrawn sources, partial matching/release without another receipt, and confirmed no-activity/unknown mapping. Private review/evidence inputs stay out. See [Project imported statements and matching history](readable-bank-tables.md).

The preceding full Release run executed 880 cases: 879 passed and the pointer concurrency case timed out waiting for its pool task to start. All twenty pointer cases passed in isolation. An independent occupied-pool experiment reproduced ordinary-task delay while a LongRunning reader started. PR #151 preserves every pointer safety assertion and gives its synchronous reader independent scheduling; the changed case passes. This supports a possible scheduling cause, not a reconstruction of the failed machine load. The latest entirely passing full suite remains 872 until another complete run succeeds. Reconciliation/tax/evidence/reports, Sheets delivery/UI, real account/device acceptance, and remaining full-build work continue.

## Explicit completed reconciliation history

Thirteen more tables retain completion/reopen provenance, frozen balances/positions/outstanding facts, selected correction choices, prior dependencies, observed/remaining errors, claims/warnings, bank coverage, errors and resolutions. Saved observation watermarks/times remain distinct from current export facts. Private explanations/evidence stay out. Three cases pass for actual reopened balances after later cash changes, actual error/correction with no journal money, and rejected hash-valid but identity-mismatched retained JSON. Windows Business Debug has no warnings/errors. The latest entirely passing full suite remains 872; tax/evidence/reports, verified Sheets publication/UI, real account/device acceptance, and remaining full-build work continue. See [Project completed reconciliation history](readable-reconciliation-tables.md).

## Explicit tax filing periods and decisions

Two more tables retain tax-period/provenance facts and ordered reviewed filing details. Unknown dates/amounts stay blank; filed zero and signed amendment amounts remain exact. Private reason/evidence inputs stay out. Two cases pass for zero filing/refund amendment beside an unknown period, and actual remittance without filing beside a reviewed Not required decision with unknown details. Filing history creates no cash posting. The latest entirely passing full suite remains 872; evidence/cancellation/financial details/reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project tax filing decisions](readable-tax-tables.md).

## Explicit managed-evidence version and link references

Three more tables retain version/hash/managed-reference/provenance facts, exact typed target/revision/status links, and releases. The existing verified internal link reader now accepts a business ID without fabricating a human session. Private labels/filenames/target names/purposes/reasons/permission scope, binaries, and raw JSON stay out. References do not grant access or imply current photo permission. Two new cases plus eight existing link cases pass without warnings for replacement/old-version release, no posting, and withdrawn photo reference-only export with access still blocked. The latest entirely passing full suite remains 872. Cancellation/financial details/reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project managed-evidence references](readable-evidence-tables.md).

## Explicit cancellation choices and approved effects

Eight more tables retain current draft and approved decision scope, ordered action/result links, resulting existing journal, frozen before/after booking facts, and separate unapplication/reduction/value choices. SourceKind distinguishes current draft from approved history. Private decision/agreement/client-reason/evidence inputs stay out; full frozen financial review remains in the archive/desktop. Two cases pass for actual mixed approval with balanced refund journal/later booking edits and status-only cancellation beside blank draft dates. Business Debug has no warnings/errors. The latest entirely passing full suite remains 872. Financial details/reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project cancellation choices and approved actions](readable-cancellation-tables.md).

## Explicit financial lender, asset, supplier, and tax details

Four more tables retain frozen lenders, asset treatment choices, selected supplier-refund lines, and exact three-way refund-tax treatment. Current draft and approved movement sources remain distinct. A shared verified approved-source reader preserves the existing movement identity/revision/date/amount checks. Private evidence and cached generations stay out. Four new cases plus three existing ledger cases pass in Release for actual lender rename/reversal, mixed tax allocation, selected supplier refund, and asset recovery. PR #151 passed both GitHub checks and merged; this branch includes its independent concurrency reader. A complete Release run and Windows Debug build are running; no complete result is claimed yet. Opening/close history/reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project retained financial details](readable-financial-details.md).

The complete Release suite now passes all 896 cases in 2 minutes 35 seconds, including prior source/table groups and the pointer-reader scheduling correction. Windows Debug has zero warnings/errors.

## Explicit opening confirmations and period-close history

Four more tables retain opening/revocation/movement facts, close/reopen provenance and exact report observation scope, and frozen reconciliation dependencies. Private explanations/evidence stay out. Two cases cover zero revocation and nonzero reversal, and actual close/reopen followed by a later transaction. The initial later transaction was incorrectly dated inside a still-reconciled month and was rejected; moving the fictional transaction to February 1 preserves the real safeguard. Latest full suite is 896 before these additions. Retained/current reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project opening and period-close history](readable-book-history.md).

## Shared exact ledger calculation for captured exports

A session-free internal ledger kernel now reads captured business/date facts, while the public owner wrapper retains authentication and uses one read transaction. Two failing-before cases exposed intermediate cent/SQLite-sum overflow despite a representable final net. Exact integer accumulation and final supported-range checks fix both; genuinely unsupported components fail explicitly. A third failing-before case exposed current opening flags in snapshot-only reads; the shared query now applies dated opening/reversal rules. Financial report profit/balance/opening calculations use the same kernel. Other domain sums remain separate work.

Five kernel cases and thirty related report/dashboard/close/history cases pass (35 targeted cases), including locked human access, fixed source generation, later-write isolation, and unchanged postings. A complete Release run and Windows Debug build are in progress. The latest completed full result remains 896. Report projections, verified Sheets delivery/UI, real account/device acceptance, and remaining full-build work continue. See [Calculate ledger reports from a captured source](readable-ledger-report-kernel.md).

The complete Release suite now passes all 903 cases in 2 minutes 15 seconds. Windows Debug has zero warnings/errors.

## Explicit current and retained ledger reports

Six more tables retain current/captured versus retained-period report scope, exact trial/profit/balance/cash facts, and ledger opening warnings. Current report timestamps/hashes stay tied to capture; retained values/dates/watermarks stay original after reopen/later writes. Public financial descriptions remain readable, while close/reopen evidence and raw snapshots stay out. Current warnings cover cash openings; remaining domain warnings/schedules are separate work. Two cases pass for actual income/fee/tax/loan/owner/cash distinctions, matching existing financial trial balances, source stability/locked access, and retained versus current reports after a later expense. Latest full suite is 903 before these additions. Remaining reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project current and retained ledger reports](readable-ledger-report-tables.md).

## Shared captured customer balance readers

Existing query-only customer/document/original-movement/receipt/invoice/credit readers now accept an internal business ID, with existing owner overloads delegating to the same calculations. Public authentication and all mutation/approval contexts remain intact. Two new cases and fifty-two existing report/application/credit/cancellation cases pass in Release (54 targeted) for exact aging/unused receipt/held deposit/funded credit facts matching the human report, later-application source isolation, locked human access/watermark preservation, and foreign-business source rejection. The initial fixture property compile was corrected to CustomerId. Latest full suite remains 903. Customer/supplier/tax and other report tables, verified Sheets delivery/UI, real account/device acceptance, and remaining full-build work continue. See [Read captured customer balances without a human session](readable-customer-report-kernel.md).

## Explicit customer balances, milestone dues, and available values

Four more tables retain current/retained aggregate customer balances, current invoice milestone dues, receipt/credit availability, and explicit detail coverage. Credit/classification rows do not add cash; held/recognized unused values remain separate. Saved close reports retain original aggregate balances and mark unretained invoice/payment detail honestly rather than recalculating it from later records. Private credit reasons/raw JSON stay out. Two Release cases pass for actual partial payment/credit/held value with exact gross/tax, source isolation/locked access, and reopened retained versus current balances with unretained detail. Latest full suite remains 903; another complete run and desktop build follow. Supplier/tax/other reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project customer balances and available value](readable-customer-report-tables.md).

The complete Release suite now passes all 909 cases in 2 minutes 15 seconds. Windows Debug has zero warnings/errors.

## Shared captured supplier balance calculations

The existing credit, bill-line, bill-balance, and supplier-group calculations now accept an internal business scope; record listing shares a static business/kind reader. Existing owner wrappers and mutation/approval/history checks remain intact. Two new cases plus thirty-seven related report/payment/refund/credit cases pass in Debug (39 targeted) for actual aging and supplier credit matching the human report, later-payment source isolation, locked human access, and no foreign-business sources. Latest full suite is 909 before these additions. Supplier/tax/other report tables, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Read captured supplier balances](readable-supplier-report-kernel.md).

## Explicit supplier balance, bill, and line reports

Four more tables retain current/retained supplier and bill balances, current gross/business line balances, and detail coverage. Payables and supplier credits stay separate, unknown due dates remain blank, and retained close facts remain original after later adjustments. Unretained historical line detail is marked explicitly and not recalculated. Two new table cases plus two supplier kernel cases pass in Release for actual paid mixed-use credit, later refund/source isolation/locked access, and retained versus current supplier adjustment with blank due dates. Latest complete suite remains 909. Tax/assets/debt/owners/bookings/dashboard report groups, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project supplier payables and credits](readable-supplier-report-tables.md).

## Shared captured tax and filing readers

The existing tax calculation moved into a scoped internal reader, and filing snapshots now share a business-ID query using captured owner names. Human reports retain owner checks and call the same formulas. Two new cases plus twenty-one tax/recovery/filing/report cases pass in Release (23 targeted) for four months of actual charged/collected/refunded/remitted timing matching the human report, later-write isolation/locked access, mixed tax treatment, reviewed write-off without cash/income, and Not required filing with unknown details. The first write-off fixture omitted required evidence; the existing safeguard rejected it, and the fictional fixture was corrected. Latest full suite remains 909. Tax/other report tables, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Read captured tax amounts and filing facts](readable-tax-report-kernel.md).

## Explicit current and retained tax reports

Four more tables retain separate tax amount components, filing period/status observations, ordered decisions with captured owner names, and explicit detail coverage. Current and saved-close facts stay separate after later recovery/filing. Known zero remains zero, unknown dates/details stay blank, missing legacy filing snapshots are marked unretained without invented rows, and unsupported filing versions stop projection. Private reasons/evidence/raw JSON stay out. Three table cases plus two tax kernel cases pass in Release for actual partial recovery/filed zero/source isolation, reopened retained history, and controlled legacy absence. Latest full suite remains 909; another complete run and Windows Debug build are running. Asset/debt/owner/booking/dashboard reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project tax amounts and filing history](readable-tax-report-tables.md).

The complete Release suite now passes all 918 cases in 2 minutes 5 seconds. Windows Debug has zero warnings/errors. PR #135 passed both GitHub checks and merged; #136 is now being checked against main.

## Shared captured asset schedule readers

Existing schedule/book-value/disposal queries now accept an internal business scope; owner wrappers and mutation/approval/evidence/history safeguards remain intact. Two new cases plus twenty-eight related asset/refund/disposal/return cases pass in Debug (30 targeted) for three-month disposal/reversal roll-forwards matching human reports, source facts after later rename/lock, registered zero versus unknown book value, and no foreign asset history. Latest full suite is 918 before these two additions. Asset/debt/owner/booking/dashboard report tables, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Read captured asset schedules](readable-asset-report-kernel.md).

## Explicit asset position, journal, and event reports

Four more tables retain register facts at report capture separately from dated book roll-forwards/disposal state, attributed journal, zero-journal events, and explicit snapshot coverage. Unknown stays blank, zero stays zero, archived facts remain visible, retained close facts stay original, and private evidence/raw JSON stay out. Two new table cases plus two asset kernel cases pass in Release for actual full cost recovery/zero retirement, unknown versus registered/book zero, locked access, and retained facts beside later register review. Latest complete suite remains 918. Debt/owner/booking/dashboard reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project asset positions, journal, and events](readable-asset-report-tables.md).

## Shared captured debt schedule reader

The existing debt calculation now accepts an internal business scope while owner wrappers and loan approval/history checks remain intact. Two new cases plus thirteen debt/lender/owner schedule cases pass in Release (15 targeted) for three-month proceeds/principal/interest/fee/reversal matching the human report, original captured lender after later rename/archive/lock, registered principal versus unknown posted debt, and empty foreign scope. Latest full suite remains 918. Debt/owner/booking/dashboard report tables, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Read captured debt schedules](readable-debt-report-kernel.md).

## Explicit debt positions and captured lender journal

Three more tables retain register facts at report capture, dated principal/interest/fees, attributed journal/effective reversal kinds, original lender capture status/facts, and snapshot coverage. Known zero and unknown stay distinct. Missing historical lender capture remains blank despite current owner-lender links. Private evidence/raw JSON stay out. Two new table cases plus two debt kernel cases pass in Release for controlled legacy attribution absence, locked access, actual repayment to zero beside retained unknown principal, and old lender names beside current rename/archive. Latest full suite remains 918; another complete run and Windows Debug build are running. Owner/booking/dashboard reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project debt positions and captured lender journal](readable-debt-report-tables.md).

The complete Release suite now passes all 926 cases in 2 minutes 27 seconds. Windows Debug has zero warnings/errors. PR #136 passed both GitHub checks and merged; #137 is now being checked against main.

## Shared captured owner schedule reader

The existing owner calculation now accepts an internal business scope and debt facts from the same capture; human wrappers retain authentication. Two new cases plus ten owner/debt cases pass in Debug (12 targeted) for actual two-owner contributions/draws/payables/reimbursements/loans matching the human report, later-contribution source isolation/locked access, legacy attribution remaining unconfirmed, and empty foreign scope. No percentage distribution or posting is introduced. Latest full suite is 926 before these additions. Owner/booking/dashboard report tables, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Read captured owner schedules](readable-owner-report-kernel.md).

## Explicit actual owner position, account, and attribution reports

Five more tables retain owner positions/account balances, attributed journal/effective kinds, selected-period flags, loan attribution/unknown principal, and snapshot coverage. They add no percentage distribution or posting. Retained figures stay original, missing historical lender attribution stays unconfirmed despite current links, and private evidence/raw JSON stay out. Two table cases plus two owner kernel cases pass in Release for actual distinct movement categories/legacy attribution/locked access and retained versus later contribution/payable figures with blank unknown debt. Latest complete suite remains 926. Booking/dashboard reports, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project actual owner movements and attribution](readable-owner-report-tables.md).

## Shared captured booking contribution sources

Existing facts/billing/financial/contribution readers now accept an internal business scope and one captured timestamp. Public wrappers retain authentication/hooks, and original invoice/reduction/application/balance consistency checks remain intact. Two new cases plus twenty booking/billing/financial/report cases pass in Release (22 targeted) for actual charges/applications/tagged cash/income/direct and unassigned costs matching the human report, later-cost isolation/locked access, stable source time/watermark/serialization, private-note exclusion, unissued unknown estimates, and empty foreign scope. Business Debug has zero warnings/errors. Latest full suite remains 926. Booking/dashboard tables, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Read captured booking contribution sources](readable-booking-report-kernel.md).

## Explicit booking contribution, journal, and cash reports

Five more tables retain contribution/charge/application/open summaries, assigned journal, tagged cash, unassigned direct costs, and snapshot coverage. Nested source IDs/watermarks/dates must agree. Unissued estimates stay blank, unassigned costs receive no invented booking, and private notes/evidence/raw JSON stay out. Two table cases plus three mixed-scope cases and two booking kernel cases pass in Release (seven targeted), including actual money distinctions, retained unknown estimates, locked access, and hash-valid altered nested booking ID/watermark/date rejection. Windows Debug has zero warnings/errors. Latest complete suite remains 926; a full run including owner/booking additions is running. PR #137 passed both GitHub checks and merged; #138 is being checked against main. Billing/planning/dashboard groups, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project booking contribution and financial sources](readable-booking-financial-tables.md).

The complete Release suite now passes all 937 cases in 2 minutes 25 seconds. Windows Debug has zero warnings/errors.

## Explicit booking invoice balances and billed lines

Six more tables retain billing observation scope, original/reduced/effective/applied/open invoice net and tax, credit links, frozen billed lines, milestone dues, and snapshot coverage. Frozen payer names remain original after customer edits; known zero dues remain visible. Two Release cases pass for actual half-credit/application/tax/source lines, source access after lock, and retained versus later balances and credit history. Missing older snapshots are marked unretained and private notes/evidence/raw JSON stay out. Latest full suite is 937 before these additions. Planning/dashboard report groups, verified Sheets delivery/UI, real account/device acceptance, and full-build work continue. See [Project booking invoice balances and billed lines](readable-booking-billing-tables.md).

## Explicit planning facts retained with reports

Six more tables retain booking/event/charge/place/appointment/recipient-link facts and snapshot coverage at current or saved-close capture. They preserve UTC instants with original offsets, unknown dates/tax/duration, named/unnamed places, and old planning facts after later cancellation/archive edits. Private notes/products/overrides/raw JSON stay out. Two Release cases pass for ambiguous-hour time facts, explicit recipients/artists, locked access/privacy, and retained versus current plans. The first fixture incorrectly expected cancellation to clear configured trial; the existing transition preserves it and the assertion was corrected without changing behavior. Latest full suite remains 937. Dashboard groups, remaining export validation/transport/UI, real account/device acceptance, and full-build work continue. See [Project planning facts retained with reports](readable-booking-planning-tables.md).

## Combined captured financial report reader

The complete report now composes all shared domain readers from one database/business/date scope and supplied timestamp, without fabricating/renewing human access. Public calls keep their read hook and check access before returning; late lock is rejected. Two new cases plus twenty-eight related financial/close/dashboard cases pass in Release (30 targeted) for full actual report equality after later writes/lock, stable nested groups/watermark/time, and rejected absent business/invalid range/late lock. Raw ReportSnapshot remains internal and can contain review/evidence inputs; only explicit projections are readable payloads. Latest full suite remains 937; another complete run and Windows Debug build are running. PR #138 passed both GitHub checks and merged; #139 is being checked against main. Dashboard projections, remaining export validation/transport/UI, real account/device acceptance, and full-build work continue. See [Read a complete financial report from one capture](captured-financial-report-reader.md).

The complete Release suite now passes all 943 cases in 2 minutes 36 seconds. Windows Debug has zero warnings/errors.

## Shared captured dashboard financial datasets

Dashboard finance/monthly/reconciliation readers now accept an internal business scope and timestamp, composing the complete captured report without human renewal. Public calls retain hooks and reject late lock. Two new cases plus eighteen dashboard/reconciliation cases pass in Debug (20 targeted) for complete actual equality after later activity/lock, unknown empty month preservation, exact aging, and enforced 120-month/date limits. Raw dashboard/report objects remain internal; allowlisted table projections follow separately. Latest complete suite is 943 before these two additions. Dashboard projections/upcoming data, remaining export validation/transport/UI, real account/device acceptance, and full-build work continue. See [Read captured dashboard financial datasets](captured-dashboard-finance.md).

## Explicit dashboard financial chart data

Eight more tables retain chart scope/basis, monthly values/activity/closed coverage, exact aging bands/milestones, expense categories, cash/reconciliation facts and source journal, and actual owner movement data. Unknown months remain blank and known zero stays zero. Two new cases plus two captured-dashboard cases pass in Release (four targeted) for actual totals/aging/cash/owner distinctions, later-activity isolation/lock, stable hashes, captured closed coverage after reopen, and reconciliation references without private close-review text. Latest complete suite remains 943. Upcoming chart data, remaining export validation/transport/UI, real account/device acceptance, and full-build work continue. See [Project dashboard financial chart data](readable-dashboard-finance-tables.md).

## Shared captured upcoming-event datasets

Upcoming/reservation/paid-milestone readers now accept an internal business scope and timestamp while preserving original selection/review/deposit/artist/undated/monthly/window rules. Public calls retain hooks and reject late lock. Two new cases plus twenty upcoming/reservation/billing cases pass in Release (22 targeted) for an actual approved fictional deposit/confirmation, whole source equality after later cancellation/lock, private-note exclusion, unknown pre-acceptance value, and rejected invalid business/window. Business Debug has zero warnings/errors. Latest complete suite remains 943. PR #139 passed both GitHub checks and merged; #140 is being checked against main. Upcoming projection, export validation/transport/UI, real account/device acceptance, and full-build work continue. See [Read captured upcoming-event datasets](captured-dashboard-upcoming.md).

## Explicit upcoming-event chart data and review sources

Eight more tables retain planning scope, monthly confirmed/known/unknown values, month membership, dated/undated events, review issues, artist state, and as-of invoice/milestone sources. Agreement gross remains separate from cash income; unknown values stay blank and undated work stays outside month counts. Private notes/products/evidence/raw objects stay out. Three table cases plus two captured-upcoming cases pass in Release (five targeted) for actual commitment/source balances versus cash, pre-acceptance uncertainty, undated/archived artist issues, privacy/lock, and later-cancellation isolation. Latest complete suite remains 943; another complete run and Windows Debug build are running. Cross-calendar date validation, complete export assembly/transport/UI, real account/device acceptance, and full-build work continue. See [Project upcoming confirmed-event chart data](readable-dashboard-upcoming-tables.md).

## Captured reports across Windows calendars

Six Arabic/Thai calendar cases now pass for full financial reports, saved close/reconciliation/ledger tables, dashboard finance, and confirmed upcoming events with actual paid milestones. Failing-before runs exposed Arabic ISO-date parse errors, Thai query years returning empty cash activity, and a remaining ledger projection reading 2027 as Gregorian 1484. Captured query/parse/identity paths now share explicit invariant Gregorian storage dates; shared writes in those files follow the same format. Other entry/validation/storage paths still need focused checks, and no old data is rewritten. Related Release checks passed all 133 cases before the last four calendar additions; the new six-case run passes. The previous complete suite passed all 952 cases; a complete run with the fix and Windows Debug build are running. PR #140 passed both GitHub checks and merged; #141 is being checked against main. Complete export assembly/transport/UI, real account/device acceptance, and full-build work continue. See [Read captured report dates across Windows calendars](captured-report-calendar-dates.md).

## Verify readable table content before assembly

A small verifier now rebuilds source-bound tables through the bounded scalar builder, checks their digests, rejects empty/duplicate/oversized sets, and freezes copies against mutable aliases. Ten new cases plus nine existing builder cases pass in Debug for changed source generation/watermark, altered names/headers/values/order/width/missing rows, literal exact amounts, immutable copies, and aggregate cell/byte limits. This verifies table integrity only; required-group completeness, dictionary, workbook creation/readback, and publication remain separate work. Latest complete suite is 952 while the calendar-fix run continues. See [Verify readable table content before assembly](readable-table-verification.md).

The calendar-fix complete Release suite passed all 958 cases in 2 minutes 25 seconds. Windows Debug had zero warnings/errors.

## Assemble all current readable table groups

One captured source now composes all 29 existing groups into 155 tables, with shared bounded financial/planning windows, a group inventory, aggregate budgets, row/group cancellation checks, and final source/hash verification. No partial assembly returns when a required group fails. Four new cases plus sixteen verifier/calendar cases pass in Release for complete Arabic/Thai source equality after later edits/lock, private-field exclusion, actual paid/confirmed wedding facts, invalid windows, cancellation, and a deliberately corrupted required group. A fixture lookup compile was corrected to existing authorized search. Latest complete suite is 958 before the verifier/assembly additions. This is table assembly only; Start Here/dictionary, ownership settings, durable artifacts, verified Sheets delivery/UI, and remaining full-build work continue. See [Assemble readable export table groups](readable-export-assembly.md).

The complete Release suite now passes all 972 cases in 2 minutes 14 seconds. Windows Debug has zero warnings/errors. PR #141 passed both GitHub checks and merged; #142 is being checked against main.

## Source, conventions, and business table inventory

Three more bounded metadata tables describe the selected source/date windows, literal exact-value conventions, private omissions, retained/current scope, and every business table's group/counts/raw UTF-8 bytes/hash. Combined budgeting includes the 155 business tables before metadata. Local assembly is explicitly separate from remote publication; ownership percentages remain unimplemented, and database-wide recovery statistics are not exported as business facts. Two new Debug cases pass for complete stable inventories/source details and rejected missing mappings/metadata budget overflow. Latest full suite is 972 before these two additions. Column dictionary, durable artifacts, verified Sheets delivery/UI, and remaining full-build work continue. See [Describe the readable export source and table inventory](readable-export-metadata.md).

Metadata, assembly, and verifier checks also pass all sixteen targeted cases in Release.

## Column dictionary with scoped join hints

Every business, metadata, and dictionary column now has an ordered dictionary entry with literal representation, shared unit/date meanings, blank semantics, and reviewed join hints. Ambiguous document/evidence versions, customer/supplier credits, bank/application groups, and tax/book periods are kept separate; polymorphic references retain kind-dependent instructions. Current register joins never replace frozen historical names/values. Unreviewed shared types stay general rather than guessed. Two new Release cases pass for complete coverage, known targets, state-versus-date distinction, rejected missing targets, and combined-budget overflow; eight metadata/assembly/dictionary cases passed before the final join additions. Latest full suite is 972 before the four metadata/dictionary additions. Durable generation format, verified Sheets delivery/UI, and remaining full-build work continue. See [Read the export column dictionary](readable-column-dictionary.md).

## Versioned readable generation manifest

Projection format 1 now combines the 155 business tables and four metadata/dictionary tables with selected source identity, date windows, group inventory, table/aggregate counts and digests, and exact business journal totals. Verification requires an independently trusted manifest digest, rechecks all table payloads, and rebuilds every count/group/journal fact. It carries no database-wide recovery facts and fabricates no session. Three new Release cases pass for all-159-table JSON round-trip after owner lock, fixed table-hash compatibility, wrong/missing/mixed-source content, malformed version/group/counts even with matching digests, and balanced totals beyond Int64 while rejecting negative/fractional/unbalanced values. Thirty related Release cases passed before the last malformed-manifest checks. Latest full suite is 972; a new complete run and Windows Debug build are running. Durable artifacts, verified Sheets delivery/UI, ownership settings, and remaining full-build work continue. See [Verify a readable generation manifest](readable-generation-manifest.md).

The complete Release suite now passes all 979 cases in 2 minutes 30 seconds. Windows Debug has zero warnings/errors. PR #142 passed both GitHub checks and merged; #143 is being checked against main.

## Bounded staged-table streams

A small UTF-8 JSON-lines codec now preserves header/row literal text and exact digests while reading one bounded row at a time. It checks expected row counts, rejects extra/malformed/changed content, and compares all descriptor counts/bytes/hash. Limits allow JSON escaping without bypassing decoded cell/text budgets; they are separate from future Google request bounds. Eleven new Debug cases pass for long Unicode/buffer crossings, literal exact/blank values, truncation/extras/types/width/digest corruption, budget/line bounds, cancellation, malformed UTF-8, and nonempty-destination rejection. Latest complete suite is 979 before these eleven additions. Safe durable file staging/loading, verified Sheets delivery/UI, and remaining full-build work continue. See [Stream staged readable tables with bounded rows](readable-table-streams.md).

The staged-table codec and manifest checks also pass all fourteen targeted cases in Release.

## Bounded staged manifest headers

A canonical manifest codec now checks a 1 MiB/16-depth header and independently trusted digest before any table payload is opened. Duplicate/unknown JSON properties, missing objects/groups, malformed dimensions/counts, invalid journal totals, truncation, and unsupported format/source are rejected; writer destinations must be new and empty. One new Release fixture passes the canonical round-trip and malformed headers, including structurally invalid manifests with matching digests. Fifteen related codec/manifest/generation cases passed before the final journal checks; the header fixture passed again afterward. Latest full suite is 979 before codec/header additions. Durable directory staging/loading, verified Sheets delivery/UI, and remaining full-build work continue. See [Check a staged manifest before reading tables](readable-manifest-streams.md).

## Durable readable generation staging and reload

The file service now writes/flushes 159 tables plus manifest in an owned generated directory, rereads every payload, and gates non-overwriting directory publication on the active store, exact inventory, and cancellation. Reload uses the caller's trusted digest and rebuilds complete source/table/count/journal verification. Eight new file cases plus fifteen codec/manifest cases pass in Release (23 targeted) for real disk round-trip through a fresh store after snapshot disposal/owner lock, no watermark/session renewal, duplicate preservation, wrong digest/path/index/ID/foreign source rejection, corrupt/missing/extra files, final cancellation/retirement, and preservation of unknown files during owned-only cleanup. Latest full suite is 979; a new complete run and Windows Debug build are running. Artifact refs still need durable job integration; Google permission fences, write/readback/publication, scheduling/UI, ownership settings, and remaining full-build work continue. No Google request occurred. See [Stage and reload a readable generation](readable-generation-files.md).

The complete Release suite now passes all 999 cases in 2 minutes 22 seconds. Windows Debug has zero warnings/errors. PR #143 passed both GitHub checks and merged; #144 is being checked against main. Current Google guidance was checked on November 12: RAW input preserves supplied values, recommended request payload is at most 2 MB, and current per-user read/write quotas are 60 requests per minute per project. The current Drive help page now lists 20 million cells or 100 MB for Google Sheets; app projection budgets remain independently smaller. These limits inform the next request/readback layer, not a claim of real delivery.

## Bounded literal Sheets value request plans

The planner verifies the captured generation, covers every header/data cell, uses RAW string values and explicit ROWS/A1 rectangles, splits both rows and oversized column spans, packs up to 100 ranges, and checks actual serialized request bodies against 1,500,000 bytes. Four new Release cases pass for exact nonoverlapping row/column coverage, literal/formula-like/extreme integer/decimal/date/Unicode/blank values, body sizes, A1 boundaries, range/cancellation limits, all 159 captured tables, and a 128-by-32,768-character escaped row that must split without losing cells. Latest complete suite is 999 before these four additions. PR #144 passed both GitHub checks and merged; #145 is being checked against main. No HTTP request occurred. Typed-cell readback, private workbook creation/schema, durable jobs/worker/publication, scheduling/UI, and remaining full-build work continue. See [Plan bounded literal Sheets value requests](sheets-value-request-plan.md).

## Typed literal cell verification

Readback now has a small verifier for both entered and effective string values. It rejects formulas even when calculated text matches, numeric/bool/error conversions, changed/missing nonblank values, and ambiguous/duplicate/unexpected fields. Omitted cells are accepted only for expected blanks. Seventeen new Release cases plus four planner cases pass (21 targeted), including the explicit matching-result formula falsifier and fixed errors without cell contents. Latest full suite remains 999 before planner/literal additions. Range/offset/coverage verification, bounded grid reads, private schema/creation, durable jobs/worker/publication, scheduling/UI, and remaining full-build work continue. No HTTP request occurred. See [Verify entered and effective literal cell values](sheets-literal-cell-verification.md).

The complete Release suite passed all 1,020 cases in 2 minutes 18 seconds. Windows Debug has zero warnings/errors. PR #145 passed both GitHub checks and merged; #146 is being checked against main.

## Scoped grid readback verification

The grid verifier now checks spreadsheet/sheet identity, title, expected offsets, rectangular bounds, row/cell coverage, and both entered/effective literal values. Missing trailing cells/rows are accepted only for expected blanks; extra, foreign, duplicate, malformed, shifted, formula, or numeric data is rejected. Fourteen new Debug cases pass; all thirty-five grid/literal/planner cases also pass in Release. Latest complete suite is 1,020 before these fourteen additions. These are controlled JSON fixtures, not real readback. Bounded read requests, transport/schema/private creation, permission/source fences, jobs/worker/publication, scheduling/UI, and remaining full-build work continue. See [Verify the workbook, range, and literal grid contents](sheets-grid-readback-verification.md).

## Bounded typed-grid read request plans

Per-table read plans now verify/freeze source content, split rows and wide column spans, and estimate both typed value copies plus escape/wrapper overhead before producing fixed-endpoint range/field-mask requests. Two new Debug cases and all thirty-seven read-plan/grid/literal/write-plan cases pass in Release. They cover exact 256-cell maximum-row coverage, estimated versus actual simulated response bytes below the hard HTTP limit, typed verification, normal row splits, fixed/no-secret URLs, cancellation, and changed digest rejection. Latest complete suite remains 1,020 before grid/read-plan additions. PR #146 passed both GitHub checks and merged; #147 is being checked against main. Estimates do not replace the HTTP response bound or real account acceptance. No HTTP request occurred. Transport/privacy/schema, durable jobs/worker/publication, scheduling/UI, and remaining full-build work continue. See [Plan bounded typed-grid read requests](sheets-grid-read-plan.md).

## Reject malformed readable Unicode

The shared readable text budget now uses strict UTF-8 byte counting and rejects invalid surrogate sequences before freezing/serialization. Both lone-surrogate cases failed before the fix; a temporary actual JSON experiment also confirmed changed round-trip strings. Three committed cases plus 45 related file/Sheets cases pass in Release (48 targeted), including exact staged emoji/composed/decomposed-accent/replacement-character preservation. Valid text and digests are unchanged; existing records are not rewritten, and other entry/import boundaries remain outside this fix. Latest full suite is 1,020; another complete run and Windows Debug build are running. Google transport/privacy/schema, jobs/worker/publication, scheduling/UI, and remaining full-build work continue. See [Reject malformed Unicode before readable serialization](readable-unicode-validation.md).

The complete Release suite now passes all 1,039 cases in 2 minutes 14 seconds. Windows Debug has zero warnings/errors.

## Literal Sheets HTTP value transport

The transport now checks/freeze-copies request rectangles, requires canonical RAW payload bytes, reconstructs pinned read URLs, and calls a supplied access fence immediately before each HTTP operation with cancellation rechecked afterward. It uses the existing no-redirect/cookie, bounded-deadline/response client; typed reads enforce source identity/offsets/coverage/literal values. Write acknowledgments are not completion proof. Nine new simulated HTTP cases plus six planner cases pass in Release (15 targeted) for actual posted JSON and independent typed cells, fresh per-request access, altered bodies/URIs, denied/cancelled access, wrong workbook, quota/revocation/redirect/oversized responses, and changed readback after a successful acknowledgment. Latest full suite remains 1,039 before these nine cases. PR #147 passed both GitHub checks and merged; #148 is being checked against main. Private creation/schema, durable jobs/worker/privacy fences/completion, scheduling/UI, and remaining full-build work continue. No real Google request occurred. See [Send literal values and verify typed grid reads](sheets-values-transport.md).

All 46 Sheets protocol cases passed in Release before four additional URI mutation cases. The transport now requires exact canonical URI text, including fragments, ports, schemes, and query parameters; all thirteen transport cases pass afterward. Windows Debug has zero warnings/errors.

## Stable readable workbook schema plan

A verified generation now maps to exact stable source sheet IDs and dimensions, metadata-first display order, conditional header freezes, and a separate 32-by-2 delivery-status tab. All 159 source tables retain their hashes; status capacity adds 64 cells. The verifier rejects missing/extra/changed IDs, names, indexes, types, grid sizes, freezes, and hidden tabs. Eleven new Debug cases and thirteen transport cases pass in Release (24 targeted). Latest full suite is 1,039; another complete run and Windows Debug build are running. PR #148 passed both GitHub checks and merged; #149 is being checked against main. No Google request occurred. Private file creation, schema initialization, jobs/worker/status publication, scheduling/UI, and remaining full-build work continue. See [Plan and verify the readable workbook layout](sheets-workbook-schema-plan.md).

The complete Release suite passed all 1,063 cases in 2 minutes 30 seconds. Windows Debug has zero warnings/errors.

## Job-owned staging schema initialization plans

The pure initialization planner now handles empty, reused-default, and unrelated-default-ID GRID layouts, adds replacements before deleting a temporary default, and applies ascending target indexes using the documented before-move semantics. A complete matching layout produces no operations; unknown partial layouts fail. The caller still must prove app/job ownership/privacy before executing the plan. Four new Debug cases and eleven schema cases pass in Release (15 targeted) using an independent simulated request interpreter and last-sheet deletion guard. Latest full suite is 1,063 before these four additions. No workbook changed and no HTTP request occurred. Private file creation/verification, schema transport, jobs/worker/completion, scheduling/UI, and remaining full-build work continue. See [Plan initialization of a job-owned staging workbook](sheets-schema-initialization-plan.md).

## Private readable workbook job bindings

Native workbook metadata now binds only generic mode/name plus opaque purpose, folder, job, generation, manifest, and state facts. Verification requires sole connected-account ownership, no shared Drive/trash/extra permissions, edit capability, exact single parent, and all bindings. Staging is the default required state; verified files require an explicit observation path. Twenty new Debug cases and fifteen folder/initialization cases pass in Release (35 targeted). Latest full suite remains 1,063 before initialization/file-spec additions. PR #149 passed both GitHub checks and merged; #150 is being checked against main. These are metadata fixtures only; no Google file was created or inspected. HTTP creation/lookup, schema transport, durable jobs/worker/completion, scheduling/UI, and remaining full-build work continue. See [Bind a private workbook to its export job](sheets-private-file-spec.md).

## Native workbook creation, lookup, and private verification transport

The Drive file helper now creates only generic empty native workbook metadata with ignoreDefaultVisibility, returns the new ID before separate privacy verification, and finds at most one fully bound job match. Duplicate/paged/incomplete/misbound lookup responses are rejected; no creation request is automatically retried. Zero matches are observations rather than proof of absence after uncertain creation. Ten simulated HTTP cases plus twenty file-spec and four initialization cases pass in Release (34 targeted), including lost-response lookup without duplicate creation, native/no-ID metadata, separate privacy checks, fixed bearer-only endpoints, invalid identities, denied access, and post-access cancellation. Latest full suite remains 1,063 before these additions. No real Google request occurred. Durable create intent/ID checkpoints, schema transport, worker/privacy/status publication, scheduling/UI, and remaining full-build work continue. See [Create and locate the job's native workbook](sheets-file-transport.md).

## Private schema initialization and independent reread transport

The schema transport now matches job/generation/digest, verifies private staging-file bindings, inspects actual layout, rechecks privacy before mutation, and independently rereads the result and privacy afterward. A matching completed layout is a no-op, including after a simulated lost update response. Seven new Debug cases and all 102 Sheets cases pass in Release for actual simulated initialization JSON, one mutation across retry, fresh access per request, source mismatch, changing privacy, withdrawn access, and changed readback. Latest full suite remains 1,063 before these additions. PR #150 passed both GitHub checks and merged; #152 is being checked against main (#151 was already merged separately). No real Google request occurred. Durable jobs/creation checkpoints, full data/status worker, pacing, scheduling/UI, and remaining full-build work continue. See [Initialize and independently verify the staging schema](sheets-schema-transport.md).

The complete Release suite now passes all 1,104 cases in 2 minutes 32 seconds. Windows Debug has zero warnings/errors.

## Immutable readable export job source records

Versioned business-scoped AppMetadata records now retain immutable source/destination/range identity and the trusted artifact digest without credentials or watermark updates. Readers check exact key/payload identity, supported fields, UTC dates, and bounds; historical schema facts remain distinct from permission to run. Ten new Debug cases plus four assembly cases pass in Release (14 targeted) for real reopen persistence after owner lock, no watermark/expiry renewal, foreign-business isolation, and corrupted version/key/generation/digest/range/timestamp/schema/mode/watermark rejection. Date selection is a public immutable DTO for upcoming controls. Latest full suite remains 1,104 before these ten cases. Reservation/creation/attempt checkpoints, worker/status publication, scheduling/UI, and remaining full-build work continue. See [Persist immutable readable export job sources](sheets-job-source-records.md).

## Permission-checked readable job reservation

Reservation now revalidates staged bytes before taking the writer, then rechecks current Sheets consent, source/store/instance/mode/schema/watermark, and destination under the transaction. Matching generation/destination reservations reuse one immutable job; conflicting persisted identity fails. Seven new Debug cases and 25 related Release cases pass for actual reopen/idempotence after owner lock, unchanged watermark/session expiry, corrupt files, wrong kind/foreign scope, changed saved source, and writer-wait cancellation, permission withdrawal, or instance replacement. PR #152 passed both GitHub checks and merged; #153 is being checked against main. Latest full suite remains 1,104 before record/reservation additions. No real Google request occurred. Creation/attempt checkpoints, delivery/status, scheduling/UI, and remaining full-build work continue. See [Reserve a verified readable export job](sheets-job-reservation.md).

## Monotonic native workbook creation checkpoints

Prepared/create-uncertain/file-known checkpoints now prevent issuing a second creation intent, retain returned IDs after permission withdrawal without granting further work, and reject changed identities, retired instances, and stale writer changes. Five initial Debug cases and 23 related Release cases passed, including a sixth competing-store case; all seven focused Release cases pass after adding invalid saved file identity coverage. Google's current native-file ID limitation and incomplete listing semantics were reread. Latest full suite remains 1,104 before job additions. No real Google request occurred. HTTP coordination, attempts, delivery/status, scheduling/UI, and remaining full-build work continue. See [Save workbook creation intent before HTTP](sheets-creation-checkpoints.md).

## Restartable native workbook creation coordinator

The coordinator now couples private-folder checks and per-request local permission fences with committed creation intent, returned-ID persistence, independent file verification, and bounded lookup after uncertain creation. Six initial Debug cases pass for lost-response recovery after actual database reopen with one POST, empty/duplicate/incomplete lookup rejection, shared-folder rejection, permission changes during access, and ID retention with no later reads after withdrawal during creation. All 24 related Release cases pass, including seven coordinator cases after adding normal-response reopen coverage. Latest full suite remains 1,104 before job additions. No real Google request occurred. Durable attempts, full schema/data/status delivery, pacing, scheduling/UI, and remaining full-build work continue. See [Recover workbook creation without duplicate POST requests](sheets-workbook-creation.md).

## Durable readable delivery attempt outcomes

Readable attempts now retain separate running, failed, cancelled, interrupted, and succeeded outcomes with policy revision, fixed failure codes, and verified file/time facts. Final completion checks current permission/job/running-attempt and matching source/file proof plus writer cancellation. Six new Debug cases and 20 related Release cases pass for actual reopen/history, no private provider details, no session/watermark renewal, duplicate/stale outcomes, and late writer cancellation/withdrawal or mismatched file/digest/attempt. PR #153 passed both GitHub checks and merged; #154 is being checked against main. Latest full suite remains 1,104 before job additions. These local result fixtures are not remote delivery proof. Publication, full worker, pacing, scheduling/UI, and remaining full-build work continue. See [Keep readable delivery attempt history](sheets-delivery-attempts.md).

## Immutable publication checkpoint and delivery status cells

A publication checkpoint now retains source/file identity and a stable source-readback time for exact status verification across retries. The separate 32-by-2 literal status table includes source/date selection, exact counts/journal totals, desktop authority, and privacy/readback limits without changing source hashes. Local success additionally requires its matching checkpoint. All nine publication/attempt Debug cases pass, including three new cases for actual reopen/retry hash stability, RAW/fixed dimensions, mismatched source rejection, late writer cancellation/withdrawal, and no business-watermark or owner-expiry renewal. The complete Release suite is running. No real Google request occurred. Remote publication, full worker, pacing, scheduling/UI, and remaining full-build work continue. See [Keep delivery status stable across retries](sheets-publication-checkpoint.md).

## Independent remote publication state confirmation

The publication helper now verifies private job bindings, updates only the app export-state property, and independently rereads full privacy/identity and verified state. Already verified state avoids another patch after a lost response. Six new simulated HTTP Debug cases pass for normal/lost responses with one patch across retry, fresh per-request access, changing sharing, unchanged state, and wrong acknowledgment identity. The full Release suite through publication checkpoint is still running. Windows Debug passes with zero warnings/errors using the documented build command; an initial unnecessary x64 publish-profile override failed because its runtime pack was not restored. No real Google request occurred. Full readback/worker, pacing, scheduling/UI, and remaining full-build work continue. See [Publish and independently check the remote file state](sheets-publication-transport.md).

The complete Release suite through publication checkpoint now passes all 1,144 cases in 2 minutes 47 seconds. Windows Debug has zero warnings/errors. All 15 related publication/attempt cases pass in Release, including the six publication-transport additions.

## Complete source and status typed readback

The composed reader now verifies full file privacy and exact layout, reads every bounded source range, and rereads layout/privacy afterward; a separate status path supports both staging and verified read-only inspection. Seven new cases initially disproved the one-request-per-tab test assumption: this fixture uses 168 source ranges plus one status range across 160 tabs. Four count assertions failed and were corrected to actual planned coverage without changing the production reader. All 15 related Release cases pass for complete coverage, final-range formula rejection despite matching effective text, late schema/sharing changes, access withdrawal, and source mismatch before HTTP. Latest full suite remains 1,144 before publication transport/readback additions. No real Google request occurred. Sequential reads are not an atomic remote snapshot. Full worker, pacing, scheduling/UI, and remaining full-build work continue. See [Read every source range and the delivery status](sheets-complete-readback.md).

## Cancellable monotonic readable request pacing

A shared in-process per-account pacer now spaces starts by at least 1,100 ms, rechecks early wakes, and releases cancelled waits without reserving a slot. All ten related Release cases pass, including three new pacing cases for 65 virtual-clock starts across minute boundaries, early wakes, and actual asynchronous cancellation/gate release. Current primary Sheets quota and .NET timer documentation were reread. PR #154 passed both GitHub checks and merged; #155 is being checked against main. Latest full suite remains 1,144 before publication transport/readback/pacing additions. No real Google request occurred. Pacing does not reserve cross-device/process quota or enable billing. Worker integration, bounded backoff, scheduling/UI, and remaining full-build work continue. See [Pace readable delivery requests](sheets-request-pacing.md).

## Paced running-attempt access and exclusive delivery lease

The access context now checks current running attempt/job/policy/source and actual token connection after pacing and again after acquiring access; private folder/file responses receive a final local fence. A separate exclusive Sheets file lease rejects competing store handles while allowing independent archive work. Five new access/lease cases and three pacing cases pass in Release (eight related) for locked-owner/background behavior, unchanged watermark, actual fictional privacy responses, no HTTP after pacing-time cancellation/withdrawal/attempt completion, lease exclusion, and reacquisition. Latest full suite remains 1,144 before these additions. No real Google request occurred. Full worker, bounded backoff, scheduling/UI, and remaining full-build work continue. See [Fence each readable delivery request](sheets-delivery-access.md).

## Complete readable export delivery worker

The worker now integrates capture/staging/reservation, exclusive lease/interrupted-attempt handling, paced current-connection access, private creation/schema, RAW writes, every source/status typed range, stable publication, and final schema/privacy/local-writer verification. Already published files receive read-only verification on retry. Initial Debug validation passed all 14 worker/publication cases using a server that applies actual posted requests and reads its stored cells, covering successful real-source capture, lost create/schema/value/publication/local-completion responses, changed cells/privacy/permission, final writer cancellation, and lease recovery. All 19 expanded Release cases pass in 2 minutes 29 seconds, adding empty lookup recovery, corrupt local source before HTTP, changed published cells without mutations, later desktop coverage, and final writer permission withdrawal. Windows Debug passes with zero warnings/errors. PR #155 passed both GitHub checks and merged; #156 is being checked against main. Latest full suite remains 1,144 before these additions; the full suite is running again. No real Google request occurred. Queue/backoff/health, scheduling/UI, retention, and remaining full-build work continue. See [Deliver and independently verify a readable workbook](sheets-delivery-worker.md).

## Report-date-aware readable coverage and health

Health now separates latest attempt, highest verified watermark, and current-schema coverage for the selected report dates/destination. Later changes remain pending, replaced folders inherit no coverage, persisted running without a live worker needs review, and success requires matching creation/publication file proof. Four new Debug cases pass using local fictional completion results, including proofless success rejection, preserved history/last success, disable/failure states, and no background owner-expiry/watermark renewal. Latest full suite remains 1,144; the complete suite through the worker is still running. No real Google request occurred. These historical local observations do not establish current remote privacy or unchanged cells. Queue/backoff, scheduling/UI, retention, and remaining full-build work continue. See [Read export coverage and the last delivery outcome](sheets-export-health.md).

The complete Release suite through the delivery worker now passes all 1,181 cases in 4 minutes 38 seconds. Windows Debug has zero warnings/errors. Report-date profile and health cases are being checked separately in Release.

## Explicit report-date and automatic export profile

Versioned instance-scoped settings now save fixed financial/planning/event dates, explicit automatic choice, 1–10,080 minute interval, owner confirmation, and revision. Saves are audited once, no-op drafts stay unchanged, restored instances have no confirmed profile, and current active-owner/profile confirmation is required for scheduling without human-session renewal. Google output permission remains separate and offline schedule withdrawal needs no Google call. Initial profile validation rejected all invalid inputs; three test assertions wrongly assumed a zero starting fixture watermark and were corrected to compare the actual baseline. All 12 profile/health Release cases pass (eight profile, four health). Latest full suite remains 1,181 through the worker. No real Google request occurred. Queue/backoff, scheduling/UI, retention, and remaining full-build work continue. See [Save report dates and an automatic export choice](sheets-export-profile.md).

## Durable readable queue records and writer comparison

Versioned instance-scoped queue records now retain profile/policy/destination, observed/delivered watermarks, preparing/attached-job state, bounded failures, retry time, and fixed diagnostics. Writes compare the exact prior record; readers reject inconsistent identity, coverage, state, retry fields, and UTC dates. A separate exclusive queue lease can hold the delivery lease. All 20 related Release cases pass, including fifteen new record/lease cases for actual reopen after owner lock, stale write preservation, thirteen corrupt records, watermark/expiry isolation, and lease exclusion/reacquisition. Latest full suite remains 1,181. No real Google request occurred. Coordinator/backoff, runtime scheduling/UI, retention, and remaining full-build work continue. See [Persist and compare readable export queue state](sheets-queue-records.md).

## Coalesced readable queue decisions and job attachment

The planner now checks current automatic-profile/Sheets consent under the writer, observes matching report coverage and retry time, preserves pending/uncertain job identities, and compares the prior queue state when attaching capture results. Current coverage needs no new generation; later changes respect the minimum interval. Changed dates preserve uncertain remote jobs but can skip untouched old-date sources without deleting them. Three initial Debug cases and all 21 related Release cases pass, including six planning cases for restart/coalescing, backoff timing, stale-profile rejection, completion throttling, and changed-date intent handling. PR #156 passed both GitHub checks and merged; #157 is being checked against main. Latest full suite remains 1,181. No real Google request occurred. Queue execution/backoff, runtime scheduling/UI, retention, and remaining full-build work continue. See [Choose the next readable export without duplicating pending work](sheets-queue-planning.md).

## Executing automatic readable exports with persistent bounded backoff

The queue now observes due/covered work, captures or resumes one source job, attaches its identity before HTTP, runs verified delivery, and retains pending changes/retry state. Failure delays double from one to sixty minutes, respecting longer configured minimums. Scheduled workers check their exact profile per request and at the final writer, preserving independent manual Sheets permission after automatic withdrawal. All eight new Debug integration cases pass using actual posted-cell server state, including locked-owner/no-renewal behavior, no HTTP for disabled/covered polls, lost-create reopen with one file, later pending changes, source/final-writer withdrawal with manual retry, eight quota backoff rounds followed by success, and queue lease exclusion. The complete Release suite and Windows Debug build are running. PR #157's conflicting-merge status was cleared by merging current main into its isolated worktree; the merge changed no files and the updated head was pushed for CI. Latest full suite remains 1,181. No real Google request occurred. Runtime/UI integration, real account acceptance, retention, and remaining full-build work continue. See [Run the saved readable export queue](sheets-automatic-queue.md).

The complete Release suite through the automatic queue passes all 1,222 cases in 5 minutes 2 seconds. Windows Debug has zero warnings/errors.

## Desktop Sheets runtime lifecycle and actual coverage display

The app now starts and stops the 15-second Sheets queue with other store runtimes before sign-in, waits for cancellation during window shutdown, and displays actual health/selected-report coverage and runtime state. Default-off consent receives a clear no-work status. Native fictional UI checks passed sign-in, lock, signed-out close, restart, and signed-in close with all runtime shutdown waits; read-only fixture checks preserve watermark 33, all financial/document/evidence facts, balanced 26,000-cent journal sides, three file hashes, no Google grant/output permission, and no Sheets metadata/cache. One additional Release case passes for disabled Sheets consent with a saved automatic choice and no capture/HTTP. Latest full suite is 1,222 before that addition. No real Google request occurred. Preview/controls, real account acceptance, retention, and remaining full-build work continue. See [Run readable export scheduling while the app is open](desktop-sheets-runtime.md).

## Verified local readable export preview

The preview service now returns captured identity/date selection, complete table/count/cell/byte inventory, exact journal facts, and notices derived from the same generation. It creates no staged files or Google/job/permission/queue records and rechecks owner access/cancellation before returning. All nine related Release cases pass, including five new cases for actual receipt totals, inventory sums, no watermark/output changes, private path omission, foreign/locked access, and late cancellation/lock/instance replacement. PR #157 passed both GitHub checks and merged; #158 is being checked against main. Latest full suite remains 1,222 before runtime/preview additions. No real Google request occurred. Desktop controls, real account/device acceptance, retention, and remaining full-build work continue. See [Preview a readable export locally](sheets-local-preview.md).

## Desktop readable preview, consent, dates, delivery, and history controls

The new panel now supports local preview/inventory/notices, fixed date and automatic settings, separate Sheets consent/private-folder setup, manual export/cancel/retry, saved coverage/queue/attempt detail, and opening the last verified workbook. Separate reload paths preserve unsaved report or Google fields; navigation keeps drafts, and only runtime text refreshes automatically. Windows Debug has zero warnings/errors. Fresh native TEST and repeat/restart walkthroughs passed preview of all 159 source tabs before Google setup, one/no-op report save, unsaved Google choice preservation through report save, navigation/reload, all unconfigured-action guards, and separate automatic-on/Google-off persistence after lock/close/restart. Read-only checks verify watermark 2, one revision-1 profile audit, no finance/document/evidence entries, no Google grant/output/job/queue, no staged readable files, and SQLite integrity/foreign keys. PR #218 passed both GitHub checks and merged; current main was merged into the completed preview branch for manual hosted run 43195594748. PR #158 passed both GitHub checks and merged; #159 is being checked against main. Latest full suite remains 1,222, with nine subsequent preview/assembly Release cases passing. No real Google request occurred. Owner percentages, other full-build requirements, real account/device acceptance, retention, and release work continue. See [Preview and deliver a readable Google Sheets export](desktop-sheets-controls.md).

## Full hosted regression and owner idle security

Hosted run [43195594748](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43195594748) passed all 1,228 Release tests through the local preview, the Windows Debug build, and recovery cryptography. This full run predates the desktop controls and subsequent owner-security changes. PRs #159–#161 and the independent lock-renewal and monotonic-idle fixes (#222–#223) have passed their checks and merged. The owner fixes reproduced failures before changing code: confirmation could revive a locked session, and a UTC-clock change could distort idle expiry. Fourteen related Release cases passed after correction. Main is now merged into this desktop-controls branch.

Throttled foreground activity checks (#224) and native idle-lock UI (#225) passed both hosted checks and merged. The actual fifteen-minute native probe kept the workspace/dialog at 871 seconds, returned to sign-in at 901 seconds, removed both, accepted fresh sign-in, and closed normally. Read-only checks preserved schema 32, watermark 33, financial/document/evidence facts, balanced journal, and three file hashes. PRs #162–#163 also passed checks and merged; #164 is being checked against main. This branch now integrates all owner activity/idle changes with the Sheets runtimes; Windows Debug and repeat native Sheets controls/restart checks pass with fixture preservation.

The separate ownership stack ([#226](https://github.com/edcadet10/sarah-beauty-desktop/pull/226) through [#231](https://github.com/edcadet10/sarah-beauty-desktop/pull/231)) implements empty schema-33 storage, exact fraction validation, verified dated/correction history, audited transactional confirmation with managed evidence, recovery validation, and native controls. The full local suite through recovery passes 1,266 cases in 5m23; integration with current owner security passes 46 related cases, both Windows builds have zero warnings/errors, and a repeat native ownership walkthrough preserves three confirmations/six shares at watermark 4 with no financial/Google records. Hosted run 43208724660 checks the integrated ownership UI branch. Readable ownership projection, password changes/recovery, live setup, remaining workflow completion, real Google consent/delivery, device checks, retention decisions, and release packaging remain unfinished. No real Google request, live financial setup, or installer release has occurred.

## Complete readable ownership coverage and writer compatibility

Four allowlisted ownership tables now join complete projection format 2: 30 groups, 159 business tables, 163 source tabs, and one delivery-status tab. Schema 34 distinguishes older coverage while preserving historical jobs/artifacts and the verified schema-33 upgrade snapshot. Two projection cases, 45 generation/schema/readback/migration cases, and 20 delivery/compatibility Release cases pass. The posted-cell server confirms actual shares/coverage before publication without exporting private reasons or changing money/watermark. Native preview, ownership review, lock and restart pass after upgrading the fictional fixture; read-only checks preserve its three confirmations/six shares and prior snapshot at watermark 4. Windows Debug has zero warnings/errors. PR #164 passed both hosted checks and merged; #165 is being checked against main. The integrated pre-projection ownership hosted run remains in progress; the full local format-2 regression is next. No real Google request occurred. See [Include ownership in complete readable generations](readable-ownership-generation.md).

The full local format-2 Release suite now passes all 1,275 cases in 6m11. The earlier integrated ownership UI [hosted run 43208724660](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43208724660) passed all 1,269 tests in 13m19, Windows Debug in 44 seconds with zero warnings/errors, and recovery cryptography. That hosted head predates the new ownership projection. [PR #234](https://github.com/edcadet10/sarah-beauty-desktop/pull/234) separately binds human sessions to their current credential after two reproduced stale-session failures; 35 related Release cases pass. Passphrase-change service/UI and the other full-build/release requirements continue.
