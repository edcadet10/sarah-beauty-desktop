# Calculate ledger reports from a captured source

The shared internal ledger calculation takes a database work scope, business ID, and date range. It reads account and journal facts without creating a human session, renewing idle access, changing the watermark, or posting money. The public desktop wrapper still requires an owner and now uses one read transaction for the account/journal queries. Export callers must use their already-authorized captured source.

Trial-balance opening/debit/credit accumulation and profit/balance totals use exact integers before conversion to supported cent values. `AccountTotal.Ending` also evaluates the exact net before conversion. A truly out-of-range reported component fails explicitly without rounding, truncation, or changing postings. Other domain aggregates have not all been converted by this slice.

Two failing-before checks demonstrated intermediate overflow: a representable ending balance failed `Opening + Debits - Credits`, and two maximum contributions followed by a reversal failed SQLite's opening sum even though its net fit. SQLite documents [integer overflow during sum](https://www.sqlite.org/lang_aggfunc.html). The changed checks pass without using floating point.

A third failing-before check found that snapshot-only ledger reads used current opening flags rather than the requested date. The shared account query now applies the same dated opening/reversal rule previously used by financial reports. Five kernel cases and thirty related report/dashboard/period/history cases pass. The complete suite and Windows Debug build are running separately. No readable workbook is published by this calculation alone.
