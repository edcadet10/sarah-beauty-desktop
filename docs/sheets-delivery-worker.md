# Deliver and independently verify a readable workbook

The worker holds the store's exclusive Sheets lease, captures and stages one consistent source generation, and reserves its immutable job and attempt. Before Google access it reloads verified staged bytes and matches every source/date-selection identity to the job. Requests use current running-attempt, policy, instance, actual connection, and account pacing checks.

For a staging workbook, it verifies the private destination, resolves creation through durable checkpoints, initializes exact schema, and writes bounded RAW batches. Each value batch has fresh private folder/file checks. It independently reads every source range, saves the publication checkpoint, writes and reads the separate status tab, promotes the remote file state, then rechecks schema and privacy before the final local writer.

An already published workbook is inspected using reads only. A lost publication response or uncertain local completion can therefore resume without changing source/status values or schema. Changed published cells fail rather than being repaired. Newer desktop changes do not change the older generation's recorded coverage.

## Preserve failures and retries

Cancellation and errors retain artifacts, file identity, checkpoints, and separate attempts. Native creation is never blindly retried: empty lookup after uncertainty stops for review. Persisted Running attempts become interrupted only after the exclusive lease is acquired. A completed job cannot start another attempt; publishing newer records needs a new generation. Runtime activity is tracked separately from persisted status.

The simulated server independently applies actual schema and RAW requests to indexed cells and returns those stored cells through the real HTTP/readback code. Initial Debug validation passed all 14 worker/publication cases. All 19 expanded Release cases pass in 2 minutes 29 seconds, adding empty lookup recovery, corrupt local source before HTTP, changed published cells without further mutations, later desktop changes, and permission withdrawal at the final writer. Timing waits are bypassed in these server fixtures; separate pacing tests check timing. Real Google consent/delivery acceptance, scheduling, controls, retention, and the remaining full build are still pending. Sequential API observations cannot prevent subsequent remote edits or sharing.
