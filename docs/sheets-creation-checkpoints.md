# Save workbook creation intent before HTTP

Each export job has a monotonic creation checkpoint: **Prepared**, **CreateUncertain**, then **FileKnown**. The writer commits the uncertain state before the caller can issue a creation request. A stale prepared checkpoint cannot authorize a second create. There is no automatic reset to Prepared.

Google does not support pre-generated IDs for native Sheets files. Its [upload guide](https://developers.google.com/workspace/drive/api/guides/manage-uploads#pre-generated-ids) therefore rules out the binary archive's pre-generated-ID strategy. Its [file listing reference](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/list) also describes incomplete and changing search results. After an uncertain create, an empty lookup does not justify another create; one fully verified job match can supply the missing ID.

Store a returned ID even if output permission was withdrawn while the request was in flight. This records an existing side effect without authorizing further Google access. Recording still requires the same immutable job and active writable store instance. Existing file identities cannot be replaced.

## Check restart and competing callers

The focused tests use actual SQLite reopen and two competing store handles. Exactly one caller can advance the prepared checkpoint. Other cases check restart, stale intent rejection, same-ID idempotence, different-ID rejection, permission withdrawal after the simulated side effect, late writer cancellation/withdrawal, corrupted checkpoints, unchanged watermark, and locked-owner expiry.

These tests have not created a real Google file. The coordinator that couples the checkpoints to HTTP, delivery attempts, data/status verification, scheduling, and controls remain subsequent work. A FileKnown checkpoint is an identity observation, not evidence that workbook contents were delivered.
