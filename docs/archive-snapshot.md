# Capture a complete recovery source

Status: Consistent database and managed-file capture is implemented. Encrypted package assembly, restore validation, and desktop recovery controls follow separately.

The capture service checks the current owner, then creates a verified SQLite snapshot in a private working directory. It uses SQLite's backup API, including committed WAL data, and makes that copy independent of WAL/SHM sidecars. The capture's business, mode, schema, store identity, and watermark come from that copy. Later live changes stay outside it.

The file list also comes from the copied database. It includes every successfully recorded issued PDF and every immutable attachment version, including earlier scans and retained private photo history. Text-only external references remain text. Unreferenced files and old backup files are not swept into the capture.

Each required file is copied while its source stream is held open. Its actual bytes and size must match the recorded hash/size where available. Paths accept only supported generated database/document/attachment names and reject reparse points. Duplicate paths, missing or changed files, invalid retained hashes, failed database integrity/foreign keys, and unbalanced posting batches prevent completion. Source readers do not open or display archived photos; normal photo permission checks remain in effect for later content use.

The manifest retains file paths, kinds, sizes, hashes, table counts, movement/line counts, and exact total journal debit/credit strings. Each posting batch must balance. Large aggregate journal checks use integer arithmetic without narrowing totals into a floating-point number.

Capture does not post money or increment the business watermark. Ordinary failures, cancellation, and disposal remove only the working files/directories created by that capture. The source store remains in place. Unexpected workspace contents or abrupt termination can leave a private staging directory; that directory is not a completed backup. File-count and total-byte limits bound capture work.

Ten new automated cases cover independently retained database/PDF/file versions, private retained-photo history, committed WAL with later live changes, missing/changed content, changed metadata, imbalanced postings, unsafe rehashed paths, foreign sessions, interrupted copy stages, and cancellation after an empty-store snapshot. The existing missing-source backup check also caught and prevented a refactor from creating a backup directory before validating its source. The full .NET suite has 537 passing cases; the Windows Debug build has no warnings or errors.
