# Wait through bounded business-store contention

Normal `BusinessStore` connections now allow 30 seconds for a busy SQLite command instead of five. [Microsoft.Data.Sqlite documents](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors#locking-retries-and-timeouts) that it retries busy/locked commands within the command timeout; 30 seconds is its default. The application still uses separate nonpooled connections, foreign keys, current-instance checks and atomic transactions. It does not rerun a business command delegate after an uncertain commit or ignore a lock error. Initialization and backup connection settings are unchanged.

GitHub run `43286179912` failed `SupplierCreditUseTests.CompetingUsesCannotSpendTheSameSupplierCreditTwice`: 1,031 cases passed and one failed while owner confirmation read the current store instance. Its stack reports SQLite Error 5. The hosted log does not identify the lock holder or how long it held the lock.

Two controlled failing-before probes hold an exclusive writer for 6.5 seconds, then release it. Owner sign-in and financial approval both failed at the same store-instance read with the five-second timeout. With the bounded 30-second wait, both succeed after release. Approval and an exact retry create one movement, one audit and one watermark change; sign-in does not change the business watermark.

Twenty-two contention/supplier-credit/activity/session cases pass after the change. Windows Debug passes without warnings or errors. Additional owner-access checks and a complete hosted regression are running before this fix is merged. This is a controlled contention reproduction, not proof of the exact hosted scheduling cause. No stored data or financial rules are changed.
