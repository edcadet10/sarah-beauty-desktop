# Read tax-recovery history with its stored Gregorian dates

The tax-recovery history reader now uses the shared invariant storage-date parser. Recovery eligibility and later-history checks retain their existing actual-date, amount, evidence and generation rules. No historical date is inferred or rewritten.

Three Arabic-calendar cases failed before the change; the matching three Thai-calendar cases passed. All 38 related cases now pass. They include cash recovery, liability offset and write-off followed by exact dated reversals, rejected excess recovery without a watermark change, restored cash/income/tax/receivable balances, and existing tax reports and document/retirement date guards. Windows Debug passes without warnings or errors.

The retirement/reversal parent passes 1,623 full-regression cases at `60deb09`. Full regression for the combined application/credit/receipt/tax changes is starting. Native release workflows, other date/aggregation boundaries and actual device/account acceptance remain pending.
