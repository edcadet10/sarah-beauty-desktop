# Project supplier payables and credits

Four source-bound tables retain supplier aging balances, bill balances, current bill-line balances, and explicit detail coverage. Report IDs join ledger report dates/mode/observation scope. Payable amounts and supplier credits remain separate; a negative bill net can represent an existing supplier credit rather than negative cash. Gross and business line balances remain distinct. Unknown due dates stay blank.

Current calculations reuse existing scoped bill/credit/payment readers. Retained period supplier/bill balances stay as recorded at close. Line detail was not retained in the close snapshot; coverage marks it `Not retained` and emits no recalculated historical lines. Reopening and later adjustments cannot rewrite the original saved report values.

Two new table cases and the two supplier kernel cases pass in Release. They cover an actual paid mixed-use bill credit with separate gross/business balances before a later cash refund, locked human access/source isolation, and original close balances alongside later supplier adjustments with unknown due dates. Private credit reasons and raw snapshots stay out. The latest complete suite remains 909 before these supplier additions. Other reports and verified Sheets publication remain pending.
