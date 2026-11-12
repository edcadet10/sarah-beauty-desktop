# Keep client-document dates consistent across calendars

Quote acceptance, invoice milestone due dates and credit-note headers now use the shared Gregorian storage-date formatter. Receipt reversal, application and refund queries use that same formatter for their issue-date boundary. Typed document snapshots, numbering and transaction boundaries are unchanged. Existing stored dates are not inferred or rewritten.

Before the fix, nine of ten Arabic/Thai-calendar probes failed. Acceptance, due and credit dates used the current calendar. Earlier receipts omitted current applications or included future applications, and Arabic-calendar receipt issuance accepted a payment that had already been reversed. The actual issuance command was exercised separately before applying the fix.

Forty-seven related cases now pass, including all ten probes plus existing quote/invoice/receipt/credit-note, payment application and dated invoice-void cases. Reversed-payment issuance rejects without another document or watermark change; receipt preview keeps only its dated history without a write. Windows Debug passes without warnings or errors.

The full historical-balance parent regression passes 1,584 cases at `f0da162`. A full regression for the combined asset/posting/document changes is running. Remaining storage-date boundaries, full release workflows and actual device/account acceptance continue separately.
