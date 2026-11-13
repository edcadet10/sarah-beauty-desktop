# Review the packaged equipment lifecycle

Date: November 12, 2026. Application: self-contained Windows x64 preview from `9b667a8`, schema 40. This extends the [fictional bookkeeping/reimbursement fixture](native-reimbursement-loan-acceptance.md). Amounts and evidence are explicit TEST inputs, not actual tax or disposal decisions.

## AC-032 native sequence

The equipment register starts with unknown cost and tax basis, while its separately approved purchase already establishes $300 book cost. The native form shows both unknown register fields blank. Through Reviewed registered asset facts, the owner reviews cost to $300 and leaves tax basis unconfirmed. The saved fact history retains previous null values, the reviewed cost, unchanged unknown basis, reason, evidence, owner, and request identity. Every posted financial row matches the checkpoint before that review; it does not create a book adjustment or depreciation.

The owner explicitly approves $30 management-book depreciation dated November 7. Independent checks find expense debit $30 and accumulated-depreciation credit $30, with no Bank change. Remaining carrying value is $270. No depreciation is calculated or posted from the unconfirmed tax basis.

The owner then approves a fictional November 8 disposal for $250, with explicit fictional zero tax, payer, method, and decision evidence. The retained movement debits Bank $250, clears accumulated depreciation $30, debits disposal loss $20, and removes equipment cost $300. The register retains the disposal date and movement identity.

## Independent and native report checks

- Original ten movements and their journal/source rows remain unchanged. The twelve final movements have 28 journal lines with balanced debits/credits of $2,695.
- Asset book value and accumulated depreciation both close at zero. Registered cost remains $300 and registered tax basis remains unknown. The fact-review history remains retained.
- Packaged P&L shows income $200, expenses $175, and result $25. Bank/assets are $1,325, debt $350, and equity including result $975; the balance sheet ties.
- The native asset schedule shows disposed Yes, closing cost/depreciation/carrying value all $0, registered cost $300, and tax basis Unknown. The second owner restarts and obtains the same schedule and reports.
- Integrity and foreign keys pass. All final customer, register, draft, movement, journal, account, and document rows match the disposal checkpoint after both owners' report checks. Unconfirmed cash openings remain explicit.

This covers AC-032's stated native fixture. It does not establish a tax depreciation policy, all disposal corrections, other asset types, physical-device behavior, or complete release acceptance. Suppliers/supplies, remaining acceptance scenarios, real Google delivery, and signing continue separately. No production code changed for this check.
