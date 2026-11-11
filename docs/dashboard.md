# Review the business dashboard

Status: The desktop dashboard, source review, and consistent datasets are implemented. Charts follow separately.

Open **Dashboard** after signing in. It captures local records automatically. Expand **Dashboard dates** to choose the financial period, planning business date, and upcoming event window, then select **Refresh dashboard**. The captured dates, mode, and watermark identify the displayed result. Invalid dates or dates changed during loading do not replace the previous capture.

Cards show posted income, expenses, result, cash by account, customer/supplier balances, tax liability, known loan principal, confirmed wedding count/value, pending work, and supply alerts. Cash cards identify unconfirmed openings and the latest applicable reconciliation. Unknown loan bases and agreement values remain separate from known subtotals.

Expand the source lists below the cards to review supporting balances, aging milestones, upcoming agreements/artists/payers, pending work, supplies, and debt/tax/owner schedules. Select a supporting account to see its captured entries: cash accounts use all entries through the financial as-of date; other accounts use the selected financial period. Section buttons update the navigation selection and preserve cached section editors. They open the relevant section; use the displayed name/identity to select its record there.

Backup health currently states that Sheets export, full recovery archives, and restore tests have never completed. Local database upgrade snapshots do not establish portable recovery.

Financial datasets use one captured database snapshot and the same business/mode, dates, and committed-data watermark as the underlying reports. The financial window supports up to 120 calendar months. Partial months use only the selected transaction dates.

Monthly income, expenses, and result use approved P&L classifications, including dated refunds/corrections, financing expenses, depreciation, and disposal results. Contributions, draws, transfers, asset capitalization, tax liabilities, and loan principal do not become ordinary income or expenses. Expense categories retain the account identities and explicit depreciation category.

An empty month remains a gap unless approved ledger activity or verified closed-book coverage supplies context. Closing a complete no-activity period can establish known zero activity. Reopening that book period removes its closed coverage; reopening a bank reconciliation is a separate action. The month dataset retains both context indicators. A partial month with verified closed coverage uses that selected portion rather than the full month's total.

Customer aging uses each remaining invoice/milestone share once. Due today and future dates belong to **Not due**. Other bands are 1–7 days overdue, 8–30 days overdue, over 30 days overdue, and an explicit unknown-due-date group. The bands reconcile to the customer amount due. Available credits and unallocated receipts remain separate report measures.

Cash datasets retain each account's posted balance, opening confirmation, and latest active reconciliation on or before the financial as-of date. They include the captured source entries supporting that balance. A later or reopened reconciliation does not give an earlier card a completed status. Retained reconciliation and closing hashes are checked before their completion context is used.

## Confirmed work and agreement value

Confirmed-event datasets use a separate planning as-of date and a window of up to 120 calendar months starting on or after that date. They count active registered weddings that are Confirmed at capture time. Cancelled, Completed, archived, and out-of-window work is excluded. Confirmed work with an undecided date stays separate from the monthly groups. Current artist identities, including archived/unavailable states, remain explicit.

Known agreement value comes from the exact current quote accepted on or before the planning date, including its recorded tax. It is a commitment measure, not additional cash or income. Unaccepted booking edits do not inflate it; changed event/charge/plan details and missing reservation/funding reviews remain visible. See [Review a changed booking reservation](reservation-reviews.md).

A credit note adjusts its linked invoice without rewriting an accepted quote. If it affects the current agreement and the effective issued charges have not been fully reissued to that agreement's net/tax amounts, the exact remaining commitment needs review. Its confirmed booking still counts, while its value stays outside the known subtotal. Review the actual commitment or issue and accept an amended agreement. Fully reissued amounts are counted once. Dated payer/milestone balances use the planning as-of date independently of the financial window.

## Work needing review

The operational dataset lists saved approval drafts, missing invoice due dates, active booking tax/schedule/reservation issues, unaccepted issued proposals, and incomplete setup reviews. Opening-account review uses the financial as-of date. Tax filing status and register facts are current at capture time; a paid remittance does not clear a filing review. Explicit reviewed zero openings and documented filing decisions clear only their own tasks.

Unclassified received value includes amounts reserved as customer credit. Reserving value does not classify it as income. Trip reimbursement tasks use reviewed requests less dated actual payment links and releases. A fully linked requested trip disappears from the unpaid queue; a later release restores its unpaid portion. Neither a dashboard read nor a payment attribution creates another expense or cash movement.

Supply summaries retain active current statuses, locations, and known expiry dates. Expiry alerts include the planning date itself. Unknown expiry remains unknown, and archived supplies are excluded. Private product notes are not copied into the dashboard.

The combined dashboard reader captures all three datasets in one database transaction. Each uses the same committed-data watermark, even when financial and planning dates differ. Concurrent writes cannot update only some cards in that captured result.
