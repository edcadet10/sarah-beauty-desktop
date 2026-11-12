# Project applications and funded credit history

Status: Explicit customer-credit, funding, application-approval, dated-application, and credit-draft tables are implemented internally as another partial readable group. Resource/banking/tax groups, reports, transport/publication, and UI follow separately.

Rows retain source payment/customer/credit identities, issued and funded amounts/tax, Reserve/Release events, refund/funding reversal links, source drafts, application invoice/milestone identities, dated reversals, and approval revision/hash/actor/time. Current credit drafts remain separate from approved credits and retain blank unknown dates. Free-form credit reasons and cached generations are omitted.

These tables preserve decisions and movements of already received value. They do not create another receipt, infer new income, or become an additional cash ledger. Apply dated reversal and Reserve/Release meaning when reading history; do not sum all positive rows as net balances. Exact current balances belong to the existing business reports and their eventual export.

Three cases pass: actual receipt-funded credit, application, and partial return with one cash receipt; actual dated application/unapplication and separate approvals; and an incomplete draft with no issued credit or reservation. Private reasons stay out. Windows Debug has no warnings/errors. The latest full suite has 863 passing cases before billing-link/ledger/credit additions. No Google request or complete-export claim occurred.
