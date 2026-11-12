# Prepare recovery in the archive's explicit mode

Recovery preparation now takes an explicit TEST/LIVE context. The default remains TEST for existing callers. The verified manifest must match that context before owner credentials are hashed or a recovered destination is created. An invalid mode is rejected when the service is constructed.

The prepared copy retains its mode and business/store identities, receives a fresh writable-instance identity, and reestablishes the selected owner's access with a new passphrase. Its audit session carries the recovered mode. Before publication, verification checks the prepared identity, profile and document hashes, balanced journal, exact record inventory and every managed file against the original verified source. Cancellation reaches this verification.

Twenty-six related cases pass across the wider preparation/older-schema run and the corrected mode-specific rerun. The new encrypted TEST/LIVE round trips retain posted money, a PDF, pinned setup evidence and profile history; reject opposite-mode selection and altered receipt mode; and cancel after access preparation without publishing another copy. Two initial round-trip assertions compared deserialized array-bearing records by reference; comparing serialized manifest content corrected the test assertions. Existing preparation/upgrade cases passed in that first run.

Windows Debug passes with zero warnings/errors. Native TEST profile reopen/lock/close and independent database/file checks preserve the prior fixture. LIVE working-copy activation and selection remain unavailable in the interface and follow separately. These temporary encrypted fixtures are not a second-device or physical-failure acceptance test.
