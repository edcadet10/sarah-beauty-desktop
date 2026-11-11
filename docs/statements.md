# Preview a statement CSV

The Statements screen currently previews rows. It does not import bank lines, match payments, or complete a reconciliation. Those steps remain in development.

1. Open **Statements**, then choose a CSV file or paste its contents. Use UTF-8 text with a header row, at most 4 MB, and at most 10,000 transaction rows.
2. Expand **Map statement columns and signs**. Enter the actual column headers for the date and description.
3. Map either one signed amount column or separate debit and credit columns. For separate columns, leave the signed amount header blank. Debit means money leaving the account; credit means money entering it.
4. Select the exact date format and delimiter. If the signed amount column uses positive numbers for outflows, select that option explicitly.
5. If you map a reference, mark it unique only when it identifies each real transaction on that account.
6. Select **Preview statement rows**. Check the source row numbers, dates, descriptions, signs, and totals against the statement.

The preview shows up to 250 rows; its totals include every parsed row. Identical rows remain separate and are flagged for review. Equal dates and amounts do not establish that a transaction is duplicated.

Amounts use USD with at most two decimal places. Standard grouped amounts such as `1,234.56` and parenthesized negative amounts are supported. Invalid grouping, invalid dates, zero transaction amounts, malformed CSV, and unsupported totals stop the preview with an explanation.

## Development references

The parser uses .NET's [TextFieldParser](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualbasic.fileio.textfieldparser?view=net-10.0) for quoted fields and multiline descriptions. The file chooser uses the [Windows App SDK picker](https://learn.microsoft.com/en-us/windows/apps/develop/files/using-file-folder-pickers) associated with the app window. No bank feed is connected.
