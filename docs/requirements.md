# Sarah Beauty Desktop Requirements

Status: Planning baseline. Application implementation and acceptance testing are pending.

This document records the agreed scope. Detailed fields are in the [data model](data-model.md), calculations in [business rules](business-rules.md), and unresolved choices in the [implementation plan](implementation-plan.md). Open choices must not be silently replaced with invented business facts.

## Application scope

Sarah Beauty Desktop will be a Windows application intended for a Surface Pro 7.

The application must:

- Support everyday operations without internet access, including financial approval, local reporting, and PDF preparation.
- Keep the main business records in a local database with locally stored attachments.
- Provide a readable, one-way export in Google Sheets.
- Store complete recovery backups in private Google Drive storage.
- Display separate last-success times and pending/error states for the Sheets export and recovery backup.
- Work with touch, keyboard, and mouse.
- Require no recurring software subscription for the agreed personal business use.
- Keep TEST and LIVE records separate and label test documents clearly.

The initial scope covers the new business only. Earlier personal-business or other-company transactions are not imported automatically. Opening contributions and expenses require specific evidence and approval.

Google Sheets and Google Drive backups require internet access. Core work must continue when the network, sign-in, API quota, or storage is unavailable. Automatic backups run while the application is open; a closed app must not be described as continuously backing up.

Phone applications, a public customer portal, automated bank feeds, payroll, tax-return filing, and retail product inventory are outside the initial release. Future product sales require an additional design before use.

## Users and approvals

The application will support two owners.

- Both owners can create and review financial entries.
- Either owner can approve an entry, including their own.
- Draft entries must not affect posted financial totals.
- Approval must validate an entry before recording it in the books.
- Records must identify the creator, approver, and approval time.
- Approvals must work offline.
- Approving the same entry again must not record it twice.
- Financial posting, corrections, document issuance, period closing, and restoration must be logged.
- Owner identity and the selected business must remain visible while working.

The local sign-in method is an open implementation decision. A selected display name alone is not proof of who approved an entry. Google sign-in is for backup authorization, not a prerequisite for local financial approval.

## Customer records

Each customer record must have:

- An automatically generated, permanent customer ID.
- A full name.
- Optional phone number and email address.
- A preferred contact method, which can remain undecided.
- Optional billing name and address.
- Optional makeup preferences, sensitivities, and private notes.
- Photo permission status and supporting permission details.
- An active or archived status.
- Creation and last-updated timestamps.

### Customer validation rules

- A name is required. Empty or whitespace-only names are invalid.
- Phone and email are optional. Normalize whitespace-only contact fields to empty.
- When both phone and email are empty, display "Contact information missing" without blocking the record.
- Clear that warning when a valid phone number or email is added. Restore it if both are removed.
- Explain invalid supplied contact details without claiming that format validation proves ownership or deliverability.
- Different customers may share the same name. Identify them by their permanent IDs.
- Customer records must be available for entry and editing offline.
- Calculate customer balances from financial records; do not allow direct balance editing.
- Archive referenced customers instead of deleting their history.
- Keep private notes, sensitivities, and permission evidence out of client documents.

## Bookings and participants

- Each booking has a permanent ID, event details, a primary contact when known, and linked documents, appointments, participants, and payment milestones.
- A wedding booking groups its trial and wedding-day appointments.
- Service recipients and invoice payers are separate roles. One person may have either or both roles.
- A booking can have one payer or multiple payers; each invoice has one identified payer.
- A payer may cover their own services, someone else's services, or both.
- Travel payer and responsibility for another person's unpaid share are explicit per-booking decisions. Undecided is valid in a draft.
- Do not issue an affected invoice until its payer and charges are resolved. No charge can be assigned twice.

### Unnamed participants

- Record the number of bridesmaids or other non-bride recipients before their names are known.
- Store unnamed places within the booking, without creating customer records with invented names.
- Link an existing customer or a new name-only customer when a name becomes known.
- Naming an existing place does not change the booked service quantity or price by itself.
- Changing the booked quantity is a separate explicit action, with document revision where needed.
- Display total, named, and unnamed places. These counts must agree.

### Booking lifecycle

Use Inquiry, Quoted, Awaiting deposit, Confirmed, Completed, and Cancelled states. Store status history and reasons; changes to a status label do not create money movements.

A trial payment alone does not reserve the wedding date. Confirmation requires the required booking deposit to be satisfied by approved payments or explicitly accepted credits, resolved event details, and a recorded reservation action. Cancellation follows a recorded decision and does not erase appointments, payments, or documents.

## Services and appointments

- Maintain configurable services, prices, durations, package components, and tax review status.
- Start with bridal makeup including a trial, non-bride makeup, and wedding travel. [Business rules](business-rules.md) define the agreed prices.
- Support custom lines, discounts, and future services without editing source code.
- Store trial and wedding locations separately, with artist, start/end times, ready-by time, and completion status.
- Start with one artist and allow more later. Flag overlapping scheduled work for the same artist, including an explicit override reason when appropriate.
- Store products/shades used, private notes, and permission-linked photos without assuming exact supply consumption.

## Quotes, invoices, receipts, and payment plans

- Prepare printable PDFs from the approved document layout. Sending documents remains a manual owner action.
- Support draft, issued, accepted where applicable, superseded, and void states with version history.
- Freeze descriptions, prices, billing details, event details, tax decisions, terms, and due dates on issuance.
- Record quote acceptance and its evidence. Catalog or customer edits must not rewrite issued documents.
- Convert accepted charges into one or several payer invoices, preserving source-line identity and preventing duplicate billing.
- Track trial, booking deposit, final, and custom milestones without charging the trial twice.
- Prepare receipts only for approved payments, and credit notes only for approved adjustments.
- Allow case-specific cancellation, retention, refund, or transfer decisions. Never invent a default cancellation fee or refund promise.
- Clearly mark drafts with unresolved tax or payer decisions; block live issuance of affected documents.

## Financial records

- Maintain balanced journal entries in integer cents and cash-basis management reports.
- Track payment methods and actual receiving/paying accounts independently.
- Support gross receipts, processor fees, payouts, cash, refunds, customer credits, and payment allocation.
- Track purchases, supplier balances, receipt evidence, expenses, assets, debts, mileage, and owner transactions.
- Separate actual owner contributions, withdrawals, loans, and reimbursements. Ownership percentages do not divide each receipt or determine withdrawals automatically.
- Track bank/processor/cash transfers without counting them as sales or expenses.
- Preserve posted records. Correct them using linked reversals or adjustments, subject to dependencies and closed periods.
- Keep unknown opening balances and asset values distinguishable from confirmed zero.
- Record and report tax decisions, liabilities, refunds, remittances, and periods without calculating a filing obligation from an unconfirmed rate or date.

## Supplies, assets, debts, and mileage

Supplies use Available, Low, and Finished states, optional opened/expiry dates, and purchase links. Exact stock valuation and automatic product consumption are not part of this model.

Assets need ownership, acquisition, business-use, cost, contribution, book-value, depreciation, and disposal records. Unknown amounts remain blank. Record approved book treatment separately from tax basis.

Loans need lender, terms, principal, interest, fees, due dates, and supporting evidence. Principal repayments must not become ordinary expenses.

Mileage needs actual date, driver, purpose, route, miles, parking/tolls, and reimbursement status. Customer-billed travel is a separate calculation.

## Reconciliation and period closing

- Import bank statements with preview, mapping, and duplicate detection, or enter statement lines manually.
- Support partial and many-to-many matches with amount limits and identifiable outstanding items.
- Show unexplained lines and differences. A net difference of zero alone is insufficient to finish reconciliation.
- Record the statement period, balances, evidence, reviewer, and completion time.
- Close a period only after blocking checks are resolved and reports are saved.
- Reject financial changes dated in closed periods unless an owner explicitly reopens the period with a reason.

## Dashboard and reports

Provide cash balances with reconciliation dates, income/expenses/result, amounts due, future confirmed weddings, low/expired supplies, debts, tax status, and pending approvals/backups. Every total must link to its supporting records.

Provide profit and loss, balance sheet, trial balance, cash movements, customer/supplier balances, owners, assets, debt, tax, and booking-contribution reports. Charts and exports must follow the definitions and date filters in [screens and reports](screens-and-reports.md).

## Backup, privacy, and recovery

Use the [backup and recovery](backup-and-recovery.md) contract for one-way Sheets exports, versioned full archives, separate success states, and restore checks. Google Sheets edits must not flow back into the books.

Real customer information, attachments, financial records, account identifiers, credentials, and backups belong outside the public repository. Use fictional examples in development and demonstrations. A private Drive folder and readable Sheets export must be shared only with the intended owners.

## Acceptance criteria

The original AC-001 through AC-006 and the remaining scenarios are maintained in [acceptance criteria](acceptance-criteria.md). They define required behavior; none is claimed to have passed in the unimplemented desktop application.
