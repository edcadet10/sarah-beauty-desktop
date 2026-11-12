# Review your business dashboard

The dashboard starts with income, expenses, and result for the displayed financial period. Open **Balances and planning** for customer, supplier, tax, loan, and cash-account figures. The review cards open work and supply details or upcoming booking details. Charts, financial sources, and capture metadata remain available in separate expandable sections.

Choose **Refresh dashboard** after changing records or dates. The displayed figures remain a captured view until refreshed. Upcoming agreements remain separate from received income. **About this dashboard** retains the exact capture time, watermark, dates, mode, and classification policy.

The previous fixed claim that Google was disconnected has been removed. Local archive health remains visible; the dashboard directs users to Backups and recovery for connection and delivery details rather than inventing their state.

## Layout checks on November 12, 2026

Claim: the summary can adapt to narrower windows without hiding or clipping its exact primary amounts. Reject the change if values change, extend outside the window, disappear from the accessible tree, or review buttons reveal the wrong section.

Native UI Automation verifies three primary columns at 1280 × 900, two at 900 × 900, and one at 500 × 900. Exact income, expense, and result values remain inside the window. An initial expectation of one column at 600 pixels was disproved: two 260-pixel cards still fit there. The breakpoint follows available card space rather than that mistaken window-width assumption. The 1280- and 500-pixel renders were visually inspected.

Opening balances and planning still reveals the expected funded credit. Both review actions expand their matching details, and the financial-source shortcut reaches Reconciliation. Fictional financial history, cancellation records, current balances, and issued PDF hashes remain unchanged. The Debug build has zero warnings/errors.

This change does not modify report calculations or capture rules. Actual Surface touch, larger Windows text, full keyboard traversal, owner task testing, and runtime theme inspection remain separate acceptance checks.
