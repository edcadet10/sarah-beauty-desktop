# Keep payment-application dates consistent across calendars

Payment application writes and protected history/retry reads now use the shared Gregorian storage-date formatter/parser. Approval, partial reversal, source/invoice generation checks and transaction boundaries keep their existing rules. Applications still consume already approved value without creating another financial movement.

All six Arabic/Thai-calendar probes failed before this change. New approval could fail against its own incorrectly dated history or report an unchanged current available amount. Reading an existing application or retry could fail parsing or change the interpreted date.

Fifty-five related cases now pass, including new approval, dated partial reversal, unchanged earlier availability, exact retries without another watermark change, existing funded-credit/payment/receipt cases and dated invoice-void/document checks. Windows Debug passes without warnings or errors. No historical date is inferred or rewritten.

The combined asset/posting/client-document parent passes 1,603 full-regression cases at `505dcce`. The later retirement/reversal parent has full local and GitHub checks running. Other date/aggregation paths, complete release workflows and actual device/account acceptance remain pending.
