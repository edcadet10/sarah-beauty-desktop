# Project booking invoice balances and billed lines

Six source-bound tables retain booking billing observation scope, invoice original/reduced/effective/applied/open net and tax, credit-note links, frozen invoice/credit lines, milestone dues, and snapshot coverage. Payer names and source hashes stay tied to issued versions, independently of later customer names. Known zero dues remain visible and unknown due dates stay blank.

Current rows use the existing billing calculation and one captured time/watermark. Retained rows keep the original close balances and credit history after later reductions. Missing older booking snapshots are marked unretained; unsupported versions stop projection. Private booking notes, evidence, and raw snapshots stay out. Report IDs join the broader ledger report scope; billed lines repeat existing document facts rather than creating another charge.

Two Release cases pass for actual half-credit/application tax and zero dues, frozen payer/source lines after a customer rename, locked access, and retained versus current balances/history after reopening and reduction. The preceding complete Release suite passes all 937 cases in 2 minutes 25 seconds; Windows Debug has zero warnings/errors. Planning/dashboard groups and verified Sheets publication remain pending.
