# Preview a statement CSV

The Statements screen previews CSV rows, records reviewed transaction identities, and imports statements. Imports do not post cash or income. Bank matching is available separately; reconciliation completion remains in development.

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

## Match bank transactions to cash entries

1. Open **Bank matching**. Select the account, effective matching date, and reason, then select **Load matching source balances**.
2. Search for a bank transaction and its actual cash journal entry. Both must use the same account and cash direction. A $120 receipt with a $3.60 processor fee has $116.40 in its cash entry.
3. Enter the positive amount to match and select **Add matching portion**. Add other portions to combine bank transactions or cash entries. Prepared portions are checked together for excess amounts.
4. Select **Review prepared matching**. Check the captured date, account, sources, amounts, and reason. Confirm your passphrase and approve the matching.
5. To undo all or part of a match, choose **Original match to undo**, enter the amount and reason, then select **Review unmatch** and approve it. The remaining source amounts refresh after approval.

Match and unmatch history retains the effective date, approver, approval time, and reason. A cash movement with active matches requires the matching dependencies to be undone before its posting can be cancelled. A cancellation reversal does not represent another bank transaction. Record an actual cash return through its refund workflow.

Matching does not complete a reconciliation. Zero net difference alone does not explain unmatched deposits and payments.

## Development references

The parser uses .NET's [TextFieldParser](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualbasic.fileio.textfieldparser?view=net-10.0) for quoted fields and multiline descriptions. The file chooser uses the [Windows App SDK picker](https://learn.microsoft.com/en-us/windows/apps/develop/files/using-file-folder-pickers) associated with the app window. No bank feed is connected.
