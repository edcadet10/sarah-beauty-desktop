# Void an unused invoice

The `DocumentDecisions.VoidInvoice` service records one authenticated, immutable decision against the exact issued invoice/hash. It requires a private reason, a date from issue through today, an open accounting date, optional verified managed evidence, and a permanent request ID. It rejects payment or credit-note history even after a payment application was fully reversed. A cancelled booking requires its cancellation review.

The transaction retains the issued snapshot, number, PDF and coverage history. It marks the cached version Void, advances invoice and booking billing generations, and records one audit. It creates no cash movement or journal entry. An exact retry returns the retained decision without advancing again; changed or competing requests cannot create another decision. Cancellation, lost access and storage failures after the writes roll back all effects.

Dated balances include the invoice before its void date. Tax reports keep the original invoice tax charge in the issue period and include its reduction in the void period. This preserves agreement when periods are combined. These fields describe app records, not a determination of a jurisdiction's filing rules. A replacement invoice for the booking cannot precede its retained invoice void dates; its original number is never reused.

Schema 37 raises the minimum writer contract so earlier writers cannot bypass these rules. Its migration adds no financial record or decision and preserves an openable schema-36 snapshot.

Forty-seven related Release cases pass, including actual encrypted invoice/PDF restore and activation, competing payment/void writers in both orders, exact retry, source/date/period rejection, and rollback after writing. Two checks failed before correction: a backdated replacement could overlap the old invoice, and a following-month void was absent from that month's tax reductions. The full regression and native controls are separate checks.
