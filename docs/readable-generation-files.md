# Stage and reload a readable generation

`ReadableGenerationFiles.Stage` verifies a generation, writes its 159 table files and manifest into a new generated staging directory, flushes each file to disk, and rereads every file through the bounded codecs. It then checks the active business-store instance, exact file inventory, and cancellation inside the final database writer gate before renaming the directory to the generation ID. Existing generations are never overwritten.

Files live in `ReadableExports` beside the business database. Paths accept generated directory IDs and known manifest/table indexes only. Reparse points, unexpected files/subdirectories, missing files, and invalid inventories are rejected. Failure cleanup removes only files created by that workspace and attempts a nonrecursive empty-directory removal. An unexpected file is preserved; no old generation or abandoned staging directory is automatically pruned.

`Load` requires the generation ID and independently trusted manifest digest from its caller. It validates the manifest before opening tables, enforces the shared decoded budget, checks every table descriptor/hash, rechecks the complete inventory, rebuilds the generation manifest, and confirms the current store identity/watermark scope. It rejects data belonging to another or retired instance. Neither operation renews a human session or changes the business watermark.

## Verify the file lifecycle

Eight new file cases and fifteen codec/manifest cases pass in Release (23 targeted). They cover:

- Real flushed files reloaded through a fresh `BusinessStore` after the source snapshot is disposed and the owner locks.
- Duplicate staging preserving the original generation; wrong digest, path traversal, invalid indexes, empty IDs, and foreign-store staging rejected.
- Changed, missing, or extra published content rejected while preserving existing files.
- Cancellation and store retirement before publication leaving no published generation.
- An unknown staging file preserved while only owned output is cleaned.

The snapshot is read-only source data; the staged output contains the allowlisted readable tables, not the database or authentication/recovery material. Staged files are local readable business data. They are disposable delivery inputs and do not replace encrypted recovery archives. A caller must persist the trusted artifact reference in its job record; queued jobs, scheduling, Google permission fences/write/readback/publication, and desktop controls are not implemented by this file service. Restoring into another instance requires rebuilding its delivery inputs.

Flush and directory rename do not establish physical power-loss or second-device acceptance. Those checks remain pending. No Google request occurred.
