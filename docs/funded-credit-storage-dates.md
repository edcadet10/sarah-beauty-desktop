# Preserve the funded-credit issue date across calendars

Funded-credit creation now uses the shared Gregorian storage-date formatter for its header. The existing funding writer and protected reader already use the matching storage convention. The credit header and reservation therefore retain the same actual issue date when the current calendar changes.

Both Arabic/Thai-calendar issue-date probes failed before this change. After the fix, 42 related cases pass, including issue, exact retry without another watermark, credit application, dated return of unused value, earlier/current availability, existing funded-credit/refund/classification and application-date cases. The complete two-calendar exercise retains one original receipt and its journal entries; credit operations do not create another income or receipt movement.

Windows Debug passes without warnings or errors. No historical date is inferred or rewritten. The combined asset/posting/client-document parent passes 1,603 full-regression cases at `505dcce`; the later retirement/reversal parent has full local/GitHub checks running. Other boundaries, complete release workflows and actual device/account acceptance remain pending.
