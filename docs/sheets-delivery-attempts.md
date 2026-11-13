# Keep readable delivery attempt history

Each attempt retains its job, writable instance, policy revision, start time, and outcome. Failed, cancelled, and interrupted attempts remain separate from later retries. Diagnostics use fixed public codes rather than provider messages or tokens. A persisted Running state does not prove a process is alive; the delivery worker must hold the store lease before marking abandoned attempts interrupted.

Completion requires the current immutable job and permission, the same running attempt/policy revision, the saved remote file ID, and matching generation, manifest digest, source watermark, and verification timestamp. The final writer rechecks cancellation. This persistence boundary consumes a worker's verification result; it does not itself read Google cells or establish delivery.

Owners can read scoped job, creation, and attempt history. Background bookkeeping does not renew their sessions or increment the business watermark. A verified job cannot begin another delivery attempt; publishing newer records requires a new generation.

## Check outcome persistence

Tests reopen actual SQLite records, preserve failed/interrupted/verified attempts, reject private diagnostics, block duplicate running/successful attempts, and check permission withdrawal, cancellation, mismatched file/digest/attempt proof, and stale completion. These are local persistence tests with fictional results. The complete remote verification worker, publication checkpoint, pacing, scheduling, and controls remain subsequent work.
