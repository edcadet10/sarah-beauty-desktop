# Build progress

This file tracks the full application build requested on November 10, 2026. A component marked implemented still needs the release checks applicable to it. The app is not ready for live business use.

| Area | Current state | Evidence or remaining work |
|---|---|---|
| Local storage | Implemented | Verified pre-upgrade snapshots, atomic migrations, explicit TEST mode |
| Owner setup and sign-in | Implemented | Two owners, salted PBKDF2-SHA256 hashes, short lockout after failed attempts, expiring store-bound sessions, transaction audit |
| Customers | Implemented | Name-only save, contact warning, validated optional contact fields, search/edit/archive, permission history, stale-edit guard |
| Bookings | In progress | Charges, places, recipient links, exact plans, appointments, and explicit paid reservation implemented; full cancellation workflow and calendar refinements pending |
| Client documents | Implemented core | Quote versions, bounded payer invoices, dated credit notes, approved-payment receipts, and saved PDFs; full release walkthrough pending |
| Books and approvals | In progress | Core posting/reports, receipt/credit applications, funded credits, cash refunds and dated reversals implemented; period-close UI and remaining workflows pending |
| Resources | In progress | Registers, supplies, trips, openings, disposals, reviewed bills, mixed supplier payments/refunds, bill credits, and credit use implemented; complete trip reimbursement pending |
| Reconciliation and reports | In progress | Financial reports, aging, tax, statement import/review, and matching controls implemented; reconciliation completion, closing, and full dashboard pending |
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
