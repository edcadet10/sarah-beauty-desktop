# Keep retained book-period dates independent of the calendar

Book-period close/history reads, reconciliation matching, earliest-date checks and reopening now use the shared Gregorian storage formatter/parser. A close saves the same typed start/end dates in its indexes and retained snapshot. Reopening a later period restores the exact earlier closed-through boundary.

Four Arabic/Thai-calendar close/reopen probes failed before the fix. Existing Gregorian dates either failed parsing or changed their interpreted year; reconciliation queries used a different calendar date and could not find the completed period-end reviews.

All 27 related cases now pass, including first close, history, reopen, two consecutive retained periods, restoring the earlier boundary, posting-date protections and captured-calendar reports. The fictional receipt remains the sole posted movement throughout each two-period exercise. Windows Debug passes without warnings or errors.

The combined asset/posting/client-document parent has a full regression running; the historical-balance parent passes 1,584 cases. Existing historical records are not inferred or rewritten. Other date/aggregation boundaries, release workflows and actual device/account acceptance remain pending.
