# Review packaged supplier bills, credits, and refunds

Date: November 12, 2026. Application: self-contained Windows x64 preview from `9b667a8`, schema 40. This extends the [fictional equipment fixture](native-asset-acceptance.md); no real supplier or funds are involved.

## AC-033 native sequence

The owner creates one fictional supplier and approves a November 9 bill for one $100 supplies line, explicitly reviewed as 100% business use, zero fixture tax, and General expense. The native bill shows $100 unpaid. Independent checks find every posted financial row unchanged; approving an unpaid operational bill does not recognize cash-method expense.

| Native event | Operational result | Book result |
| --- | --- | --- |
| Actual $100 business-bank payment, November 10 | Payable $0; supplier credit $0 | Bank credit $100, original expense debit $100 |
| Approve a $20 supplier bill reduction, November 11 | Net bill $80; paid $100; supplier credit $20 | No movement, journal, account, or settlement changes |
| Actual $20 cash refund linked to the original payment, November 12 | Net paid $80; payable $0; supplier credit $0 | Bank debit $20, original expense credit $20 |

The refund follows the original reviewed classification and source line. It does not create service income or another expense. The original bill, reduction, payment, refund, and linked settlements remain retained separately.

## Independent checks and reports

Read-only recomputation matches the line's gross/business share and tax, approved credit, signed settlements, source-payment identity, and exact debit/credit accounts. All twelve prior movements and their source/journal rows remain unchanged. Fourteen final movements produce 32 journal lines with balanced debits/credits of $2,815.

Packaged supplier balances show zero unpaid/credit. P&L shows income $200, expenses $255, and result negative $55, including net supplier expense $80. Bank/assets $1,245 equal liabilities $350 plus equity including result $895. The second owner restarts and sees the same bill, supplier balances, P&L, and balance sheet. Complete final customer/register/draft/ledger/account/settlement rows match the refund checkpoint. Integrity and foreign keys pass; earlier unknown openings and tax basis remain unconfirmed.

This covers the stated native AC-033 fixture. Mixed-use, owner-funded purchases, other supplier adjustments, devices, real Google delivery, signing, and complete release acceptance remain separate work. No production code changed for this check.
