# Packaged payment allocation acceptance

Date: November 12, 2026. App source: `8dda1a7`; self-contained Windows x64 build. Schema 40. All records are fictional TEST data.

This extends the [same-booking split-payer fixture](native-split-payer-acceptance.md) with native AC-025 payment applications. The existing accepted quote, voided original invoice, two issued split invoices, source coverage, milestones, decisions, and four PDF hashes remain unchanged. The booking total is $752.91; active invoices are bridal $340.44 and guest $412.47.

## Native actions and expected results

1. Approve two bridal-payer receipts, $400 and $250, with explicit fictional zero tax. Bank and income each increase by $650; no processor fee is entered.
2. Apply $100 from the first receipt to the bridal invoice. Attempt its remaining $300 against the guest invoice without an on-behalf decision. Preview/actual approval reject it, and every posted source/application/value-state row remains exact.
3. Edit that same saved application, record an explicit fictional on-behalf reason, review, and approve. The $400 receipt now funds both invoices exactly once.
4. Apply $112.47 from the second receipt to the guest invoice with its own on-behalf reason. The guest invoice is settled by two receipts; automatic milestone distribution remains bounded.
5. Attempt another cent from the exhausted first receipt against the still-open bridal invoice. Attempt another cent from the second receipt against the settled guest invoice. Both actual approvals reject; posted movement, journal, batch, application, group, and value-state rows match their prior checkpoints.

## Independent results and restart

Three approved application groups allocate $512.47. The first receipt is exhausted; $137.53 from the second remains unapplied. Bridal invoice due is $240.44 and guest due is zero. Each milestone stays within its issued amount; applications create no additional money or income. Four journal lines total 65,000 cents on each side, with Bank $650 and income $650. Integrity and foreign keys pass.

The second owner restarts, sees both rejected extra-cent drafts, and runs Customer aging, P&L, and Balance sheet. The bridal aging row shows $104.14 due today, $136.30 future, and $137.53 unallocated; guest balances are zero. P&L shows $650 income, zero expenses, $650 result. Assets and equity including result each equal $650, with zero liabilities. Raw retained rows, document snapshots, coverage, milestones, decisions, and all four PDF hashes match the final checkpoint. Cash-account opening warnings remain visible; no real opening is inferred.

An earlier UI Automation attempt stopped before the second receipt was created; the database confirmed only the first receipt before retry. A read-only report harness initially expected the wrong due-date buckets; inspection of retained milestone dates and independent remaining-amount sums established the actual $104.14/$136.30 split. Corrected native assertions pass. Neither interrupted attempt was counted as a pass, and neither required product changes.

This covers the stated AC-025 native scenario, not concurrent credit use or every correction workflow. Physical devices, real Google, signing, and complete release acceptance remain open.
