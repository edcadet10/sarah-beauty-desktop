# Keep opening-balance dates consistent across calendars

Opening-balance writes and history reads now use the shared Gregorian storage-date formatter/parser. The first-ordinary-movement check parses stored movement dates with the same helper. Confirmed zero remains distinct from unknown; revocation retains the original date and history. No historical date is inferred or rewritten.

All six Arabic/Thai-calendar probes failed before this change. New openings stored calendar-specific dates, existing history failed parsing or changed its interpreted year, and a valid earlier opening could be rejected after a calendar change.

Thirty-four related cases now pass, including all six probes, existing opening/posting-date checks and retained book-period close/reopen cases. New and revoked zero openings create no money. An opening before the first ordinary movement succeeds, while a later opening still rejects. Windows Debug passes without warnings or errors.

The combined asset/posting/client-document parent has a full regression running; the historical-balance parent passes 1,584 cases. Other date/aggregation boundaries, release workflows and actual device/account acceptance remain pending.
