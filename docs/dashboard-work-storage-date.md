# Preserve the received-value review date boundary

The dashboard's unclassified-receipt query now uses the shared Gregorian storage-date helper. The work item includes both unallocated value and its reserved credit share; future receipts stay outside the requested planning date.

The Arabic probe failed before this one-line change because the query omitted an existing receipt. The Thai probe already passed: the later availability filter excluded future value even though the initial query used a different calendar year. After the change, both probes and all 29 related dashboard/captured/readable cases pass. Windows Debug builds with zero warnings and errors; reads leave the watermark and financial movements unchanged.

The limited source scan now finds only display formatting and the invariant helper among simple `DateOnly.Parse`/`ToString("yyyy-MM-dd")` expressions in the Business project. This is not proof of every date or financial boundary. The complete regression is starting; remaining native/resource, aggregation, release and actual-device checks continue.
