# Review the agreed bookkeeping fixture in the packaged app

Date: November 12, 2026. Application: self-contained Windows x64 preview from `9b667a8`, schema 40. This is a fresh fictional TEST business on the development machine, with two owners, one payer, one zero-start loan, and one equipment register entry. No real funds or business facts are involved.

## AC-029 native workflow

The owner prepares, saves, reviews, and approves each of the seven agreed movements through Money:

| Movement | Amount |
| --- | ---: |
| Owner contribution | $1,000.00 |
| Loan proceeds | $500.00 |
| Service receipt, explicitly fictional zero tax | $200.00 |
| General expense | $80.00 |
| Equipment purchase | $300.00 |
| Loan principal repayment, no interest/fees | $100.00 |
| Owner withdrawal | $50.00 |

All fixture transactions are dated November 2, 2026. The native November profit-and-loss report shows income $200, expenses $80, and result $120. The balance sheet shows assets $1,470, liabilities $400, and equity including result $1,070, with the equality message. Trial balance shows Bank debits $1,700, credits $530, ending $1,170; Equipment debits/ending $300. The second owner restarts the packaged app and obtains the same reports.

## Independent checks and limits

Read-only recomputation from the seven retained movements and 14 journal lines finds balanced debits/credits of $2,230, Bank $1,170, Equipment $300, debt $400, net contributed equity $950, result $120, and total equity $1,070. Assets equal liabilities plus equity. Each expected movement kind/amount occurs once; owner, loan, and asset links remain retained. There are no issued client documents. Integrity and foreign-key checks pass; customer, register, movement, journal, and account rows match the pre-restart checkpoint after both report runs.

The loan's register opening is explicitly fictional zero. Cash account openings remain unconfirmed and visible in report warnings. Equipment registered cost/tax basis remain unknown after approving its separate $300 book purchase; no depreciation or tax basis is invented. These are the agreed isolated arithmetic results, not certified balances or tax advice for a real business.

An initial harness assertion compared Bank directly with the ending amount in a five-column row. It was corrected to check opening, debit, credit, and ending columns in order, then both owners passed. That failed assertion is not counted as a passed run. This covers AC-029's native fixture; reimbursement, loan components/limits, other acceptance scenarios, devices, real Google delivery, signing, and full release remain separate work.
