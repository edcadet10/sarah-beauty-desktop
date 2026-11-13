# Keep possible-duplicate receipt review consistent across calendars

Possible-duplicate receipt queries and their returned payment/reversal dates now use the shared Gregorian storage formatter/parser. The existing business, date, amount and source exclusions, detail limit and owner access rules remain unchanged. Finding a possible duplicate does not post or change money.

Both Arabic/Thai-calendar probes missed the existing same-date receipt before this change. After the fix, 31 related cases pass, including identical count/details/reversal date, unchanged watermark and financial movements, existing similarity checks, receipt classification and posting cases. Windows Debug passes without warnings or errors.

The retirement/reversal parent passes its full 1,623-case regression at `60deb09` in 8 minutes 27 seconds; its GitHub run is still active. No historical date is inferred or rewritten. Other boundaries, complete release workflows and actual device/account acceptance remain pending.
