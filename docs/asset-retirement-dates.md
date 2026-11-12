# Keep retirement and reversal date guards calendar independent

Asset retirement and dependent-recovery reversal queries now use the shared Gregorian storage-date formatter. The bank-match history and cash-line readers used by movement reversal use the corresponding invariant parser. The existing business, asset, journal, date and active-match protections remain in place.

Four of eight Arabic/Thai-calendar retirement probes failed before the change. A Thai-calendar approval could retire an asset before an already posted acquisition or reverse retirement before a later recovery's reversal date. Arabic-calendar approvals could reject a valid disposal or reversal. After fixing those query boundaries, one valid reversal exposed a separate bank-match cash-date parser failure; that dependency is corrected in the same workflow.

Forty-seven related cases pass across the completed retirement/asset/bank-matching run and two added active-match checks. They cover blocked early disposal/reversal with unchanged watermark/state, valid later disposal/reversal, exact restored cost/depreciation, and active bank matches that still require unmatching before reversal. Windows Debug passes without warnings or errors.

The combined asset/posting/client-document parent passes its full 1,603-case regression in 8 minutes 35 seconds at `505dcce`. Full local and GitHub regression for the latest changes is starting. No stored history is inferred or rewritten. Other date/aggregation paths, full release workflows and actual device/account acceptance remain pending.
