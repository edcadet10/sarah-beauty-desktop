# Review booking charges and contribution

Status: The consistent billing source snapshot is implemented. Direct cost/contribution calculation and the desktop report follow separately.

Booking billing sources retain each issued invoice's payer, original lines, source quote version, and document hash. Credit notes reduce only their linked invoice. Replacing an older fully credited invoice under a revised agreement therefore does not subtract the older credit from the replacement again.

The as-of date applies separately to invoice issue, credit-note issue, applications, and partial application reversals. Effective net charges and tax reconcile to applied value plus remaining invoice/milestone balances. Shared receipts and funded credit applications can settle a booking's charges without becoming another cash receipt. Current booking facts remain labeled separately from the issued payer and charge snapshots.

The planned contribution estimate will use issued net charges after their linked reductions, excluding sales tax, less explicitly assigned direct costs. It will identify the issued-charge basis rather than call an accepted quote received income. Unissued work, unassigned costs, and simple supply statuses cannot establish exact profit or per-client product consumption. Cash received and invoice value applied need separate measures and date labels.

Reading billing sources creates no invoice, application, receipt, journal, or audit movement. The source snapshot retains its business/mode, as-of date, generation time, and committed-data watermark. Private booking notes are excluded.
