# Keep automatic archive work pending

Status: The durable queue engine is implemented. Desktop scheduling controls and the recurring app-lifetime loop follow separately. This change alone does not start a recurring task.

## Observe committed changes

Each authorized poll reads the current business watermark under a writer reservation and saves one operational queue record for this instance. Repeated changes coalesce into the latest observed watermark. The next poll discovers changes committed between observations. Queue and job updates do not advance the business watermark.

A successful archive covers its captured watermark, even when later changes arrive during encryption. Those later changes remain pending. Polling unchanged, covered data does not create another archive. A successful manual archive for the same instance and recipient can also establish coverage. Saved pending state and retry times survive restarting the app.

## Run one automatic attempt

The engine holds an exclusive filesystem lease beside this instance's database for the complete automatic attempt. Another worker returns Busy without starting a job. Closing the lease releases it; the zero-byte lock file can remain. The lease applies to automatic work. Owners can still request a separate manual archive.

The first uncovered authorized poll can run immediately. Subsequent work waits for the configured interval after completion. Failure retries wait 1, 2, 4, 8, 16, 32, then at most 60 minutes. Cancellation also preserves pending work and releases the lease. Changing the policy or recipient resets the older policy's retry state.

## Preserve uncertain results

After acquiring the lease, a later poll can identify saved automatic Running records whose earlier execution ended. It retains those attempts as Interrupted with the fixed code CompletionUnconfirmed. It does not claim their files failed encryption or passed verification. Existing files remain available for review. A new verified archive uses a different filename.

The queue runs from saved public consent and does not renew human sessions. Revoked/unavailable permission stops new work. A changed or restored instance does not inherit this instance's queue or enabled schedule. No archive is deleted automatically, and this engine does not imply Google delivery or a second-device restore test.

## Verification

Eight fictional cases cover disabled consent, coalesced changes/restart, interval boundaries, later capture changes, unchanged locked access, persisted exponential retry, exclusive leases, uncertain published history, cancellation, and changed policy/instance. The interval test first advanced from the start of a real encryption attempt and checked several seconds before completion-based eligibility; advancing beyond the actual interval corrected the test's assumption.
