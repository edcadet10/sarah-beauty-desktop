# Project customer balances and available value

Four source-bound tables retain customer balances, current invoice milestone amounts due, current receipt/credit availability, and explicit detail coverage. Report IDs join the ledger report scope table for dates, mode, observed watermark, and time. Unknown due dates stay blank. All amounts are exact integer cents; this projection does not assign a currency.

Current calculations reuse the existing scoped availability readers. Recognized unused receipts, held deposits, and funded credits remain separate. Credit rows link their funding payment and have `AdditionalCashReceipt=false`; reviewed receipt classifications also do not add another cash receipt. Available tax remains separate from available gross value. These tables describe existing value rather than creating income or cash.

Retained period reports keep their saved aggregate customer balances. The close snapshot did not retain per-payment or invoice-milestone detail; coverage marks those groups `Not retained`, and no detail is recalculated from potentially changed later records. Empty historical detail therefore does not claim a zero balance. Private credit reasons and raw snapshots stay out.

Two fictional Release cases pass for an actual partial taxed invoice payment, funded credit and held deposit with exact tax splits, source isolation/locked access, and retained versus current balances after reopening/application with unavailable detail marked explicitly. The latest complete suite is 903 before subsequent ledger/customer report additions. Verified Sheets publication and other report groups remain pending.
