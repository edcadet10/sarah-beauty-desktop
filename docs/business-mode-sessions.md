# Bind owner sessions to their business mode

Owner setup and sign-in now bind the session to the store's explicit mode. Every protected access checks that binding before credential validation or activity renewal. Matching owner/business/store/instance IDs and a current credential cannot authorize a session from another mode. A denial here does not lock a session that belongs to another context. The internal binding is not serialized.

Four injected-session probes initially passed reads, activity, passphrase confirmation and profile writes with a mismatched mode. All 21 mode/idle/activity/credential cases and 20 additional sign-in/passphrase/concurrency cases pass after the central guard. Undefined mode values are rejected before creating a session.

Windows Debug has zero warnings/errors. Native TEST sign-in, private profile reopen, owner lock and graceful close pass, preserving schema 39/watermark 5, three reviews, managed-file hash and no financial/Google records. Store opening and creation still support TEST only; this increment prepares mode binding without enabling a LIVE workspace. Explicit mode initialization, mode-aware documents, clean creation and native selection follow separately.
