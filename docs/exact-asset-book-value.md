# Read an exact retained asset book value

Asset book-value reads now sum individual journal debits and credits with `BigInteger`, group by the original account identity/code, and convert only final account and combined values to the existing supported cent range. Unknown history, negative cost, invalid accumulated depreciation and business/asset/date filters retain their existing rules. No stored journal or asset fact is rewritten.

The failing-before probe uses approved purchase, complete supplier refund and new purchase commands. The final cost is valid. Two temporary journal indexes exercise debit-first and credit-first input orders. Credit-first passed; debit-first raised SQLite integer overflow before the final cancellation was applied. Both now return the exact same cost, depreciation and carrying value and permit an unchanged-watermark disposal preview.

Thirty-two related cases pass, covering that index-order probe, a controlled out-of-range preview error, asset refunds/returns, journal attribution, private asset facts, captured reports and dated schedules. Windows Debug passes without warnings or errors. The preceding historical-balance fix has a full regression running; the earlier discard-command parent passes 1,575 cases.

The implementation follows the exact-integer approach already used in the ledger report reader. [SQLite's aggregate documentation](https://www.sqlite.org/lang_aggfunc.html#sumunc) explains why integer `sum()` can overflow before canceling inputs are reached. Other financial aggregation paths and release/device/account acceptance remain separate work.
