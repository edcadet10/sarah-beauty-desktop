# Review the business dashboard

Status: The consistent financial datasets are implemented. Operational summaries, charts, and the desktop dashboard follow separately.

Financial datasets use one captured database snapshot and the same business/mode, dates, and committed-data watermark as the underlying reports. The financial window supports up to 120 calendar months. Partial months use only the selected transaction dates.

Monthly income, expenses, and result use approved P&L classifications, including dated refunds/corrections, financing expenses, depreciation, and disposal results. Contributions, draws, transfers, asset capitalization, tax liabilities, and loan principal do not become ordinary income or expenses. Expense categories retain the account identities and explicit depreciation category.

An empty month remains a gap unless approved ledger activity or verified closed-book coverage supplies context. Closing a complete no-activity period can establish known zero activity. Reopening that book period removes its closed coverage; reopening a bank reconciliation is a separate action. The month dataset retains both context indicators. A partial month with verified closed coverage uses that selected portion rather than the full month's total.

Customer aging uses each remaining invoice/milestone share once. Due today and future dates belong to **Not due**. Other bands are 1–7 days overdue, 8–30 days overdue, over 30 days overdue, and an explicit unknown-due-date group. The bands reconcile to the customer amount due. Available credits and unallocated receipts remain separate report measures.

Cash datasets retain each account's posted balance, opening confirmation, and latest active reconciliation on or before the financial as-of date. They include the captured source entries supporting that balance. A later or reopened reconciliation does not give an earlier card a completed status. Retained reconciliation and closing hashes are checked before their completion context is used.
