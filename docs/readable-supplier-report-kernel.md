# Read captured supplier balances

Existing query-only supplier-credit, bill-line, and bill-balance calculations now accept an internal business ID. The record reader has a shared static business/kind query, and the existing owner overload uses the same query. Supplier grouping now lives in a small shared report reader. Public sign-in, payments, credits, approvals, and history validation retain their existing owner requirements and calculations.

Previously authorized captured sources can calculate payable and supplier-credit balances without opening a writable store or creating/renewing a session. Overdue, due-today, future, and unknown-date payable amounts stay separate. Supplier credits remain separate from money owed, and later payments stay outside the captured source.

Two new kernel cases and thirty-seven related report/payment/refund/credit cases pass in Debug (39 targeted cases). They compare actual aging and a paid-bill credit against the human report, preserve source facts after another payment and owner lock, and return no foreign-business records. The complete Release run before these additions passes all 909 cases; Windows Debug has zero warnings/errors. Supplier table projection and other report groups follow separately.
