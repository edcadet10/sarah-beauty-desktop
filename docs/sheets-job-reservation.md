# Reserve a verified readable export job

Reservation loads the exact staged manifest and table files using the independently trusted artifact digest. It checks the current Sheets permission before reading files and again inside the writer transaction. The writer checks the current business, writable instance, store mode, schema, destination, and source watermark.

The same generation and destination reuse one immutable job. A conflicting persisted source fails rather than replacing the job. Reservation stores bookkeeping without changing financial data or the business watermark, and it does not renew an owner session. Owners must sign in to read job history.

## Check the boundary

The seven reservation cases use fictional Google connection fixtures and actual staged files. They cover reopen and idempotence after owner lock, corrupt bytes, changed persisted identity, wrong output kind, foreign owner/business, and cancellation, permission withdrawal, or instance replacement while waiting for the writer. Failed reservation preserves existing artifacts and creates no job.

This boundary does not create a Google workbook. File creation intent, remote ID checkpoints, attempts, delivery, status publication, scheduling, and controls remain subsequent work. A successful reservation is not proof of remote delivery or continuing permission.
