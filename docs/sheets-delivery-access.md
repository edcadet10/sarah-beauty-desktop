# Fence each readable delivery request

The delivery access context waits for its shared account pacing slot, then checks the current running attempt, immutable job, policy, source instance, and token connection. It checks again after obtaining access and never fabricates or renews a human session. Private checks independently inspect the current output folder and saved workbook ID, then recheck local authority after the responses.

A dedicated file lease serializes complete Sheets deliveries across store handles. It is separate from the archive-delivery lease. The future worker must hold this lease before interrupting abandoned attempts or creating/writing workbooks.

Five new access/lease cases and three pacing cases pass in Release. They cover fresh access with actual fictional private HTTP responses, owner lock and unchanged watermark, permission withdrawal/cancellation/finished attempt during pacing with no following HTTP, two store handles competing for one lease, and independent archive/Sheets leases. Tests bypass pacing time only in the access context; the separate pacing tests check timing. The complete worker, bounded backoff, scheduling, and controls remain subsequent work.
