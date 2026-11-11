# Review the asset schedule

1. Open **Reports** and choose **Asset schedule**.
2. Enter the movement period start and the period end/as-of date, then select **Run report**.
3. Expand **Asset book values** and **Asset period movements** to review the captured amounts. Each table uses pages of 100 records.
4. Choose a **Supporting asset** and select **Show captured asset facts and sources**. Review current register facts separately from the book values, then expand its supporting entries and events.

The same schedule is available under **Captured financial reports** when reviewing a saved book-period closing. It uses the retained snapshot rather than today's business records. Earlier report versions without this schedule display that it was not captured.

Choose a movement period and an as-of date. The schedule retains each asset's posted opening cost and accumulated depreciation, period increases and reductions, and closing cost, accumulated depreciation, and carrying value. Cost increases include evidenced opening entries and reversals; they do not all represent new purchases. Accumulated depreciation added or cleared can include disposal reversals; it is not a tax deduction calculation.

An asset without approved book entries through the as-of date has an unknown book value. A registered cost does not substitute for a posted value. An asset with approved entries that have reduced its value to zero has a known zero value. Where an asset's first entries fall within the period, its posted opening is zero because no earlier approved entries exist.

Disposal status follows approved decisions and their dated reversals as of the report date. A zero-value retirement remains a supporting event even when it has no journal lines. Archived assets remain available in the schedule.

Current register facts, including legal owner, acquisition and in-service dates, registered cost, tax basis, business use, and funding, are labeled separately. They describe the register at capture time. They do not certify historical tax treatment or replace the ledger. See [Review registered asset cost and tax basis](asset-facts.md).

Supporting entries retain their individual asset and movement identities. A shared payment's cash line is not divided among assets without an actual individual source link. Supporting events also retain decisions without journal lines.

The schedule is captured in the same database snapshot as the trial balance and other reports. New period closes retain its version and original facts. An older closing report without an asset schedule must display that the schedule was not captured; reopening it must not substitute newer business records.
