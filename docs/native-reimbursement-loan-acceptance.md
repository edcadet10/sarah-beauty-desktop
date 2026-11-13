# Review packaged reimbursement and loan safeguards

Date: November 12, 2026. Application: self-contained Windows x64 preview from `9b667a8`, schema 40. This extends the [fictional bookkeeping fixture](native-ledger-acceptance.md) through the native app. No real payments or policy decisions are represented.

## AC-030 owner reimbursement

The owner approves a fictional $40 owner-paid expense dated November 3. Independent journal checks find expense debit $40 and owner-payable credit $40; Bank remains $1,170. The November 4 reimbursement debits that same owner's payable and credits Bank $40. Expense stays $120 total, payable clears, and Bank becomes $1,130. Repayment is not another expense.

The owner attempts a further $0.01 reimbursement dated November 5. Approval rejects because owner payable would become negative. Complete posted movement, journal, account, customer, and issued-document rows match the checkpoint before that attempt. Its unapproved draft remains retained.

## AC-031 loan components and historical limits

The owner approves a $55 payment dated November 5, explicitly entering principal $50, interest $4, and fee $1. Independent checks find one Bank credit $55 and separate debits to loan principal $50, interest expense $4, and loan-fee expense $1. Remaining principal is $350.

| Native invalid approval | Result |
| --- | --- |
| $350.01 principal on November 6 | Rejects a negative principal balance on that date |
| $1 principal on November 1, before the November 2 proceeds | Rejects the historical negative balance even though the current debt could cover $1 |

Both rejected approvals leave every posted financial row unchanged. Their drafts remain unapproved, without a posted movement or journal line.

## Reports, restart, and limits

The final ten approved movements produce 22 journal lines with balanced debits/credits of $2,365. Packaged P&L shows income $200, expenses $125, and result $75. Bank is $1,075, equipment $300, debt $350, and owner payable zero. The balance sheet ties assets $1,375 to liabilities $350 plus equity including result $1,025. Full Bank, owner-payable, and principal trial-balance rows agree. Both owners obtain these reports after restarting.

Read-only checks preserve the original seven movements and all their journal/source rows; after the second-owner run, all final register and financial-draft rows also match. Three rejected drafts remain, alongside ten posted drafts. Integrity and foreign keys pass. Cash openings and equipment register cost/tax basis remain unconfirmed; the loan register retains its explicit fictional zero opening.

This covers native AC-030/031 in the stated fixture. Other reimbursement policies, assets, suppliers, device behavior, real Google delivery, signing, and remaining full release acceptance are separate work. No production code changed for these checks.
