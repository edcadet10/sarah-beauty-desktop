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
