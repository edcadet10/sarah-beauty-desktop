# Keep retirement and reversal date guards calendar independent

Asset retirement and dependent-recovery reversal queries now use the shared Gregorian storage-date formatter. The bank-match history and cash-line readers used by movement reversal use the corresponding invariant parser. The existing business, asset, journal, date and active-match protections remain in place.

Four of eight Arabic/Thai-calendar retirement probes failed before the change. A Thai-calendar approval could retire an asset before an already posted acquisition or reverse retirement before a later recovery's reversal date. Arabic-calendar approvals could reject a valid disposal or reversal. After fixing those query boundaries, one valid reversal exposed a separate bank-match cash-date parser failure; that dependency is corrected in the same workflow.

Forty-seven related cases pass across the completed retirement/asset/bank-matching run and two added active-match checks. They cover blocked early disposal/reversal with unchanged watermark/state, valid later disposal/reversal, exact restored cost/depreciation, and active bank matches that still require unmatching before reversal. Windows Debug passes without warnings or errors.

The full local regression passes 1,623 cases in 8 minutes 27 seconds at `60deb09`; GitHub run `43285009768` is still active. No stored history is inferred or rewritten. Other date/aggregation paths, full release workflows and actual device/account acceptance remain pending.
