# Preview a statement CSV

The Statements screen previews CSV rows, records reviewed transaction identities, and imports statements. Imports do not post cash or income. Use Bank matching and Reconciliation to review the imported transactions against the books.

1. Open **Statements**, then choose a CSV file or paste its contents. Use UTF-8 text with a header row, at most 4 MB, and at most 10,000 transaction rows.
2. Expand **Map statement columns and signs**. Enter the actual column headers for the date and description.
3. Map either one signed amount column or separate debit and credit columns. For separate columns, leave the signed amount header blank. Debit means money leaving the account; credit means money entering it.
4. Select the exact date format and delimiter. If the signed amount column uses positive numbers for outflows, select that option explicitly.
5. If you map a reference, mark it unique only when it identifies each real transaction on that account.
6. Select **Preview statement rows**. Check the source row numbers, dates, descriptions, signs, and totals against the statement.

The preview shows up to 250 rows; its totals include every parsed row. Identical rows remain separate and are flagged for review. Equal dates and amounts do not establish that a transaction is duplicated.

Amounts use USD with at most two decimal places. Standard grouped amounts such as `1,234.56` and parenthesized negative amounts are supported. Invalid grouping, invalid dates, zero transaction amounts, malformed CSV, and unsupported totals stop the preview with an explanation.

## Import reviewed rows

1. Select the actual statement account. Enter the statement period and its confirmed opening and closing balances. Opening plus the signed source rows must equal closing.
2. Select **Review statement import**. The review captures those contents, mapping, account, and balances. Form changes require a new review.
3. Review all source rows using **Next source rows** and **Previous source rows**. Decisions remain in the review when you change pages.
4. For each repeated or potentially overlapping transaction, choose its existing transaction or **New distinct real transaction**, and enter the reason. Search candidates by description, reference, or record code. **Use matching unique bank references** links only references explicitly marked unique that match date and amount.
5. Confirm your owner passphrase and select **Import reviewed statement**. The saved statement retains its owner, time, source rows, and decisions. It creates no ledger movement.

Open a saved statement under **Imported statements** to review its original source rows. Overlapping exports can refer to the same bank transaction while retaining both source descriptions. One statement cannot link the same transaction twice. A repeated attempt to import identical CSV contents points you to the existing statement.

If a statement confirms no transactions, leave CSV contents empty, select **The statement confirms no transactions**, and enter its evidence reference. Confirm equal opening and closing balances, then review and approve it. The saved statement contains zero transaction rows and creates no ledger money. A populated CSV cannot be hidden by selecting no activity.

## Match bank transactions to cash entries

1. Open **Bank matching**. Select the account, effective matching date, and reason, then select **Load matching source balances**.
2. Search for a bank transaction and its actual cash journal entry. Both must use the same account and cash direction. A $120 receipt with a $3.60 processor fee has $116.40 in its cash entry.
3. Enter the positive amount to match and select **Add matching portion**. Add other portions to combine bank transactions or cash entries. Prepared portions are checked together for excess amounts.
4. Select **Review prepared matching**. Check the captured date, account, sources, amounts, and reason. Confirm your passphrase and approve the matching.
5. To undo all or part of a match, choose **Original match to undo**, enter the amount and reason, then select **Review unmatch** and approve it. The remaining source amounts refresh after approval.

Match and unmatch history retains the effective date, approver, approval time, and reason. A cash movement with active matches requires the matching dependencies to be undone before its posting can be cancelled. A cancellation reversal does not represent another bank transaction. Record an actual cash return through its refund workflow.

Matching does not complete a reconciliation. Zero net difference alone does not explain unmatched deposits and payments.

## Complete a reconciliation

1. Confirm the account's opening balance in **Opening balances**. An unknown opening is different from a confirmed zero.
2. Import the statement and match its actual business transactions. Post a missing receipt, fee, or other real movement in **Money**, then match it.
3. Open **Reconciliation**, choose the statement, enter the review reason, and select **Load reconciliation sources**. Work in statement date order, with consistent balances between periods.
4. Support each real deposit in transit or outstanding payment with its full remaining amount, explanation, and evidence. Select book corrections already posted when relevant; their amounts are already included in the ledger balance.
5. If the bank made an actual statement error, choose its row under **Document an actual bank-statement error**. Enter the bank evidence and explanation, then select **Add or update statement error**. This claims the row's full unmatched remainder.
6. If the bank has corrected an error, select the original error and the actual opposite-direction bank transaction under **Link actual bank corrections**. Enter the correction amount, evidence, and explanation. A partial correction leaves the unresolved amount to carry into later statements.
7. Select **Preview reconciliation**. Check the raw and adjusted statement balances, bank-error adjustment, ledger balance, outstanding evidence, and source issues. Every item must be explained and the difference must be zero. A bank-error decision creates no ledger entry.
8. Confirm your passphrase and select **Complete reviewed reconciliation**. Completion retains the reviewed snapshot and prevents changes to covered cash dates, matching, or imported bank lines.

Bank errors and correction portions cannot also match business cash. The original error evidence remains after the bank fully corrects it. To change a completed reconciliation, open its retained snapshot, enter the reopening reason, and confirm your passphrase. Reopen later reconciliations first. Reopening preserves the old snapshot; a revised completion creates another record.

After saving new records in another section, use **Reload this section** to refresh a cached source list. Reloading asks before discarding unsaved fields.

## Correct an incorrect import

1. Open the saved statement under **Imported statements** and compare its source rows, signs, period, account, and balances with the actual statement.
2. Reopen covered book periods and reconciliations first. Unmatch any otherwise unreferenced source lines, including future matches. A transaction shared with another active statement stays active.
3. Under **Correct an imported statement**, enter the reason and evidence for the correction. Confirm your passphrase and select **Withdraw incorrect statement import**.
4. Check the retained withdrawal reason and original rows. Withdrawal creates no cash movement and does not delete the import.
5. Load the corrected source, fix its mapping or balances, and review a new import. The same CSV contents and reliable references can be reused after withdrawal; duplicate active imports remain blocked.

The original import remains marked **Withdrawn** in history. Retrying its old import request returns that same withdrawn record and does not reactivate it.

## Development references

The parser uses .NET's [TextFieldParser](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualbasic.fileio.textfieldparser?view=net-10.0) for quoted fields and multiline descriptions. The file chooser uses the [Windows App SDK picker](https://learn.microsoft.com/en-us/windows/apps/develop/files/using-file-folder-pickers) associated with the app window. No bank feed is connected.
