# Project issued document snapshots

Status: Explicit issued-document, line, milestone, receipt-detail, and receipt-application tables are implemented internally as another partial readable group. Relational invoice/credit-note/payment links, other domain groups/reports, transport/publication, and UI follow separately.

Document rows retain each issued version's exact identity, number, source draft, mode, client/business/event facts, dates, public terms, related document, amounts, hash, current recorded status, and issuance/acceptance actor/times. Old accepted/superseded versions remain separate. Current customer edits do not rewrite frozen client details. Rows retain recorded PDF status, relative path, and hash; this projection does not freshly verify file bytes.

Child tables preserve line order, charge identity, quantity, copied price/discount/net/tax/total/rounding cents, and frozen milestone amounts/dates. Receipt tables retain the client-safe payment description/reference, applied invoice numbers/milestones, refunds, available payment/credit, and invoice balance as recorded when issued. These are historical snapshot balances, not current account balances.

The source's retained-hash checks precede projection. Snapshot kind/number/version/date/mode must also match relational identity. Private customer fields, internal payment memo/reference/evidence, acceptance-evidence text, and PDF error diagnostics are not selected. No raw document JSON or binary PDF is exported.

Four cases pass: frozen client facts after customer edits, exact tax/gross/available/due amounts and PDF references, actual credit-note linkage/line rounding, separate amended accepted/superseded versions, and schemas for empty tables. Private sentinels stay out. Initial test construction used incorrect credit-draft fields, and the receipt fixture needed its real tax classification before applying a taxable invoice; both were corrected without changing business rules. Windows Debug has no warnings/errors. The latest full suite has 853 passing cases before planning/schedule/document additions. No Google request or complete workbook claim occurred.
