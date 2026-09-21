# Business Rules and Calculations

Status: Planning baseline. Values below are agreed service defaults; examples contain no client records. Tax exclusions in examples are arithmetic fixtures, not live tax determinations.

## Amounts, dates, and rounding

- Currency is USD. Persist money as signed 64-bit integer cents; entry forms accept a nonnegative amount plus a movement type.
- Parse money and decimal quantities with decimal arithmetic, not binary floating-point arithmetic. Reject invalid dates, overflow, and unsupported currencies before financial mutation.
- Round calculated monetary lines to the nearest cent, midpoint away from zero. Reverse the exact original cents rather than recalculating them.
- Separate transaction date, service date, issue date, due date, tax-relevant date, and posting timestamp.
- Use America/New_York for business dates/display and UTC for audit timestamps. Store appointment timezone/offset, handling daylight-saving ambiguities explicitly.
- Unknown is not zero. An undecided due date is not automatically overdue.

## Starting prices

| Item | Default | Rule |
|---|---:|---|
| Bridal package | $300.00 | Includes trial and wedding-day bridal makeup |
| Trial portion | $150.00 | Part of the package, paid first |
| Remaining bridal portion | $150.00 | Package less trial, not an extra charge |
| Non-bride makeup | $120.00 per person | Bridesmaids and other non-bride recipients |
| Wedding travel | $1.00 per round-trip mile | Origin is the privately configured business address |
| Trial travel | $0.00 | No trial travel charge under the starting policy |

Prices and approved exceptions are configurable. Catalog edits populate new drafts; issued documents retain saved prices. Due dates and cancellation outcomes remain per booking until defaults are agreed.

## Booking totals and participants

```text
service subtotal = bridal package + sum(non-bride quantity x unit price)
                   + approved add-ons - approved discounts
travel = round(round-trip billable miles x travel rate, 2)
net charges = service subtotal + travel
client total = net charges + explicitly determined tax
named places + unnamed places = booked person-based service quantity
```

Trial and wedding appointments use the same bridal package entitlement, not two $300 charges. Naming a place changes neither its quantity nor its charge. Changing headcount requires an explicit charge review.

Discounts cannot exceed applicable charges. Allocate booking-level discounts across lines in cents using a deterministic residual rule before tax calculation. Use approved credits/refunds rather than negative invoices caused by excessive discounts.

## Trial, deposit, and final balance

For a booking with a trial, after the total and trial amount on the same tax basis are resolved:

```text
remainder = total cents - trial cents
deposit cents = round(remainder x 50%, midpoint away from zero)
final cents = total cents - trial cents - deposit cents
trial cents + deposit cents + final cents = total cents
```

Trial must be between zero and the total. A booking without a trial uses an explicitly selected plan; it does not silently inherit a bridal trial charge.

Calculate the booking-level plan once, then assign amounts to payer shares. Do not repeat the whole trial/deposit on each invoice. Shares sum to each milestone and the total. Prorating uses integer cents, a recorded weighting basis, and a stable residual recipient.

Resolve package-component, travel, and installment tax treatment before using a taxed live plan. The invoice total and milestone total must use the same tax basis.

| Arithmetic fixture, excluding tax | Total | Trial | Deposit | Final |
|---|---:|---:|---:|---:|
| $300 + 9 x $120 + 76.6 x $1 | $1,456.60 | $150.00 | $653.30 | $653.30 |
| Same agreed total increased by $0.01 | $1,456.61 | $150.00 | $653.31 | $653.30 |
| Custom $325.01 + 3 x $137.49 + 12.34 x $1.25 | $752.91 | $150.00 | $301.46 | $301.45 |

The custom travel line rounds to $15.43. These fixtures isolate price and rounding behavior.

## Invoices and payment allocation

Every accepted booking charge has a stable source ID. Quote versions and invoice lines preserve it. Issued invoice portions cannot exceed the accepted quantity or amount, including tax. Full conversion accounts for all charges exactly once; partial conversion exposes what remains unbilled.

```text
invoice open amount = issued charges + debit adjustments
                      - credit notes - net payment applications
                      - net customer-credit applications
payment available = gross receipt - refunds - net active applications
                    - value still assigned to customer-credit funding
milestone outstanding = scheduled amount - net applications to that milestone
```

Milestone applications subdivide invoice applications; do not subtract them again. Credit notes adjust charges; customer credits represent available customer value. They cannot count the same benefit twice.

Moving received value into a customer credit reserves it against the original payment through a funding record, so it cannot also be allocated as unused cash. A credit-note reduction of an unpaid charge alone creates no funded customer credit. If credit-funded money is later refunded, reduce its funding reservation and record the original payment's refund together; this consumes the value once. Applying the credit to another invoice keeps its source funding reserved. Explicitly returning unused credit to payment availability needs a linked funding release.

An application cannot exceed source availability or target balance. Paying on another customer's behalf needs an explicit relationship/decision. Overpayments remain separately identified and classified, not hidden negative invoice balances.

A refunded/moved application needs its own dated reversal. Refunding a still-valid charge reopens the amount due. If the charge is cancelled too, its credit note removes that obligation. Validate the combined change rather than subtracting a refund twice.

Customer balance sums invoices for which they are the payer. Receiving a service does not create a debt. Display unused credits and unallocated receipts separately.

## Payment methods, fees, and payouts

Payment method (cash, bank transfer, Zelle, card, or another configured method) and actual account are separate. A method is not proof of receipt. Require date, gross amount, receiving account or approved owner-clearing treatment, payer, and review evidence.

For a fictional $120 service receipt and $3.60 processor fee, excluding tax:

| Event | Debit | Credit |
|---|---|---|
| Gross receipt | Processor clearing $120.00 | Service income $120.00 |
| Fee | Processing expense $3.60 | Processor clearing $3.60 |
| Payout | Bank $116.40 | Processor clearing $116.40 |

The customer receives $120 credit. The payout creates no new sale. Applicable tax is separated from income into the relevant liability.

## Cash-basis management books

The starting management policy recognizes ordinary receipts and paid ordinary expenses according to approved classification. Unpaid invoices/bills remain operational balances. Assets, prepayments, refundable deposits, and tax exceptions need explicit treatment. See the general cash-method distinction in [IRS Publication 538](https://www.irs.gov/publications/p538); software settings do not establish tax-return treatment.

| Movement | Accounting rule |
|---|---|
| Ordinary service receipt/advance | Cash/clearing against income, separating applicable tax |
| Refundable security deposit | Cash against an approved liability classification |
| Unclassified receipt | Identified suspense balance pending review; no invented service income |
| Paid ordinary expense | Approved expense/components against cash or clearing |
| Asset acquisition | Approved asset against cash, owner funding, or debt |
| Business-account transfer | Debit receiving asset, credit sending asset |
| Owner contribution / withdrawal | Equity movement, not revenue / business expense |
| Loan proceeds / principal repayment | Liability movement, not revenue / ordinary expense |
| Loan interest/fees | Separately classified from principal |
| Tax collected / remitted | Liability movement, not service income / ordinary expense |
| Supplier refund | Reverse relevant expense/asset/tax treatment, not service income |

Owner-paid purchases require an explicit contribution or reimbursement-payable classification. Later reimbursement reduces an already recognized payable without expensing the purchase again. Unknown opening values, ownership, and tax basis remain unconfirmed. Ownership percentages are private settings; record actual contributions/withdrawals individually.

```text
each posted journal: sum(debits) = sum(credits)
trial balance: all debits = all credits
balance sheet: assets = liabilities + equity
period result = recognized income - recognized expenses
ending cash = confirmed opening cash + net posted cash movements
loan principal = opening principal + proceeds - principal repayments
asset carrying value = approved cost/additions - accumulated depreciation - disposals
```

Reject negative loan principal or available credit, including at historical dates affected by backdated changes. Never infer tax basis or automatically allocate taxable profit from the ownership-percentage field.

Bookkeeping fixture: $1,000 contribution + $500 loan + $200 service receipt - $80 expense - $300 asset purchase - $100 principal repayment - $50 withdrawal leaves $1,170 bank cash, a $300 asset, $400 debt, $950 net contributed equity, and $120 current result. Assets $1,470 equal liabilities $400 plus equity $1,070.

## Cancellation, credits, refunds, and corrections

- Store the issued terms, request date, decision, reason, approval, and evidence.
- Refunds, retention, transfers, and no-show charges are case-by-case. A date calculation does not authorize an outcome.
- Link every refund to its original receipt and remaining refundable amount.
- Transferring received value creates no new cash or duplicate income. Track whether its value was already recognized or held as a liability; post only the approved reclassification/reversal.
- Credit availability is issued value plus valid restorations minus applications, refunds, and explicitly approved expiry adjustments. Preserve each movement.
- Protect posted records and issued versions from editing. Corrections use dated linked reversals/adjustments; resolve dependent refunds, uses, or matches before reversing a parent.
- Closed-period reopening needs an owner, reason, and timestamp. Preserve and version affected reports.

## Bank reconciliation

Match signed ledger cash movements and statement lines using bounded amounts. Partial/combined matches are allowed. A payout still needs reconciliation of its processor-clearing account.

```text
adjusted statement = statement closing balance
                     + deposits in transit - outstanding payments
                     + evidenced statement-side corrections
adjusted ledger = ledger balance before corrections + approved book-side corrections
difference = adjusted statement - adjusted ledger
```

Book-side corrections are posted once before completion, not kept as unexplained balancing plugs. Completion requires zero difference AND all statement lines explained AND every unmatched ledger movement specifically supported as outstanding. Unexplained +$25 and -$25 must not pass merely because they net to zero.

Once corrections are posted, the refreshed ledger closing balance already includes them. Use that refreshed balance directly; do not add the corrections again.

## Tax and mileage boundaries

Ohio's taxable personal-care definition includes applying cosmetics. See [Ohio Revised Code 5739.01(B)(3)(o)](https://codes.ohio.gov/ohio-revised-code/section-5739.01). Location, rate, registration, timing, component treatment, and exemptions require confirmed setup; this document supplies no live rate.

For resolved tax components, retain taxable base, rate, jurisdiction, effective date, calculation/rounding rule, and evidence. Keep charged, collected, refunded, and remitted tax separate; cash-method income reporting does not determine sales-tax filing timing.

Customer travel uses quoted round-trip miles and price. Actual business mileage and reimbursement/deduction use their own records and approved rules. The $1 customer price is not a tax mileage rate.
