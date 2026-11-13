# Keep delivery status stable across retries

The worker reserves a publication checkpoint after independently reading all source cells. It binds the job, source watermark, manifest digest, generation, file, and source-readback time. The checkpoint is immutable and reused across later attempts. This makes the expected status cells reproducible after an uncertain remote completion.

The separate **Export delivery status** tab contains 32 rows and two literal columns. It identifies the source and date selection, exact row/cell/byte and journal totals, file, readback checkpoint, and desktop authority. It also explains that spreadsheet edits are not imported and privacy can change after delivery. The 159 source tables and their hashes remain independent of delivery status.

Saving the checkpoint does not mark an attempt successful. Completion additionally requires the matching checkpoint and saved file identity. The worker still must read back status cells, verify remote state and privacy, and pass final local permission/cancellation checks.

## Check persistence and status generation

Three new cases plus the six attempt cases pass in Debug. They cover exact status hash after actual SQLite reopen/retry, RAW payload construction, fixed grid dimensions, immutable source identity, late writer cancellation/permission withdrawal, unchanged watermark and owner expiry, and separate running attempt state. The full Release suite is running. These fixtures do not demonstrate real Google delivery. Remote publication, the complete worker, pacing, scheduling, and controls remain subsequent work.
