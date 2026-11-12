# Read a complete financial report from one capture

The combined financial report now has an internal database/business/date path that composes the shared ledger, customer, supplier, asset, debt, owner, booking, tax, filing, and opening-warning readers. One supplied generation time flows into its nested booking snapshots. Current facts remain tied to the same captured database and watermark after later writes.

Human calls still check owner access, retain their trial-read hook, and check access again before returning. A lock during a read therefore rejects the human-facing result. Captured callers use their previously authorized source without creating or renewing a session. Absent businesses and invalid date ranges fail explicitly.

Two new cases and twenty-eight related financial/close/dashboard cases pass in Release (30 targeted). They compare the entire actual combined result with the human report after later writes/lock, verify stable repeated results and all nested groups, and reject late human lock/missing business/invalid range. The raw combined report can contain private review/evidence inputs: it remains an internal calculation and must never be serialized as a readable export. Only the explicit allowlisted table projections are publication payloads. The latest complete suite remains 937; another complete run and Windows Debug build are in progress.
