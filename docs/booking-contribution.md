# Review booking charges and contribution

Status: Consistent billing and financial source snapshots are implemented. Combined contribution calculation and the desktop report follow separately.

Booking billing sources retain each issued invoice's payer, original lines, source quote version, and document hash. Credit notes reduce only their linked invoice. Replacing an older fully credited invoice under a revised agreement therefore does not subtract the older credit from the replacement again.

The as-of date applies separately to invoice issue, credit-note issue, applications, and partial application reversals. Effective net charges and tax reconcile to applied value plus remaining invoice/milestone balances. Shared receipts and funded credit applications can settle a booking's charges without becoming another cash receipt. Current booking facts remain labeled separately from the issued payer and charge snapshots.

Recorded direct costs use approved expense lines explicitly linked to the booking. They include such recorded costs as supplier expenses, owner-funded costs, and processor fees, with their returns/reversals. Loan interest/fees, depreciation, and asset disposal losses remain separate from this direct-cost measure. Asset purchases, principal, owner funding/draws, and tax liabilities are not direct expenses. An unassigned cost is not divided between bookings automatically. Trip reimbursement links and simple supply statuses do not create another expense or establish exact product consumption.

Recognized service income remains separate from held deposits and invoice charges. Receipt/refund source amounts retain gross, tax, fees, and net cash separately, including dated posting corrections. Receipt classification and invoice/credit applications create no additional receipt. A posting reversal corrects the books; it is not another physical customer payment. Shared receipts without a booking tag are not automatically assigned in full to every booking they pay.

The planned contribution estimate will use issued net charges after their linked reductions, excluding sales tax, less explicitly assigned direct costs. It will identify the issued-charge basis rather than call an accepted quote received income. Unissued work and unassigned costs cannot establish exact profit. Cash received and invoice value applied need separate measures and date labels.

Reading billing sources creates no invoice, application, receipt, journal, or audit movement. The source snapshot retains its business/mode, as-of date, generation time, and committed-data watermark. Private booking notes are excluded.
