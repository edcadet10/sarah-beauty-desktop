# Project billing and payment source links

Status: Explicit invoice, credit-note, milestone-reduction, and receipt-payment link tables are implemented internally as another partial readable group. Financial/resource groups, reports, transport/publication, and UI follow separately.

Seven tables retain invoice payer/accepted-quote links, billed charge portions and source signatures, invoice milestone IDs/amounts/due dates, credit-note/invoice links, credited line quantities/amounts, credited milestone/tax reductions, and receipt-to-posted-payment identities. Stable IDs connect these rows to issued document snapshots and later financial tables. Blank due dates remain unknown. Exact stored quantity text and Int64 amounts do not pass through floating-point conversion.

A small shared reader accepts only explicit trusted SELECT lists, checks column count, and uses the bounded scalar builder. It does not enumerate arbitrary business tables, export JSON blobs, or accept user SQL. Binary/unsupported results and mismatched projection shapes fail instead of silently changing the payload. Reads use the same captured source and change no business state.

Three cases pass: actual invoice/partial-credit source links and exact reductions; an actually posted payment and issued receipt without another cash movement; and rejected wrong-shape/binary projections. An initial build overlapped the preceding full test run and failed on its locked output; the completed full suite passed all 863 cases, and the sequential rebuild/new cases pass. The Windows Debug build is checked separately. No Google request or complete-export claim occurred.
