# Recover workbook creation without duplicate POST requests

The coordinator verifies the current private output folder, commits creation intent, sends the native metadata request, and saves its returned ID before checking continuing permission. Each HTTP access callback checks the current immutable job and Sheets policy before and after acquiring access.

A known file is independently verified. An uncertain create performs a bounded job lookup and adopts only one fully bound, private match. Empty, duplicate, or incomplete results preserve uncertainty and send no creation request. Every returned observation also requires a final folder and local permission check. Verified workbook state is observable but does not authorize overwriting it.

## Check lost responses and permission changes

The simulated HTTP tests combine actual staged generations, jobs, SQLite checkpoints, and database reopen. Successful and lost create responses both result in one POST across retry. Other cases cover empty/duplicate/paged lookup, a shared folder, policy withdrawal inside token access, and a returned ID retained after policy withdrawal during creation with no subsequent HTTP reads.

These checks do not establish real Google account behavior. Data/schema delivery, durable attempts, remote status publication, pacing, scheduling, and controls remain subsequent work. The caller must also fence the actual token's connection identity and serialize complete deliveries.
