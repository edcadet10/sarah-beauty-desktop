# Preserve cancellation dates and reviewed source history

Cancellation request/decision dates now use Gregorian ISO storage. Cancellation summaries and source snapshots parse retained request, decision, closed-period and receipt-reversal dates with the shared storage helper.

All eight Arabic/Thai probes failed before the change. Mixed cancellation approvals stored different calendar years; existing history and original receipt-reversal dates threw or changed dates. A source review also misread the actual closed book boundary. The closed-boundary fixture uses a real reviewed period close, rather than inserting a simulated setting.

After changing two production files, all 40 related Release cases pass, including actual mixed refund/funded-credit/charge/application effects, exact retry, retained snapshot hashes, original balances, readable cancellation history and book-period dates. Windows Debug builds with zero warnings and errors. The parent complete regression passes 1,665 cases at `38a69ac` in 7 minutes 19 seconds.

This change does not rewrite earlier stored data or choose a cancellation policy for the business. Native mixed-cancellation, remaining resource/date boundaries and complete device/release acceptance continue separately.
