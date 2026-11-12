# Stream staged readable tables with bounded rows

The staged table codec writes one UTF-8 JSON array per line: the column header first, then one string-only data row per line. Escaped newlines remain inside values. Formula-like strings, Unicode, blank values, and exact integer text retain their original table digest after decoding.

The reader accepts a seekable stream starting at zero, a trusted table descriptor, the generation identity, and the shared generation budget. It reads the expected row count, rejects extra rows, rebuilds the bounded scalar table, and compares all recorded counts/bytes/hash. It does not deserialize an unrestricted whole-generation document.

Limits are 128 cells per row, 32,768 UTF-16 code units per cell, 26 MiB per serialized line, and 512 MiB per table file. The line/file caps allow JSON escaping overhead; decoded content still has the existing one-million-cell and 64 MiB raw UTF-8 generation limits. These are local staging limits, not Google API request limits. Requests must have separate serialized-size bounds.

The writer requires a new empty seekable destination. Cancellation is checked through row budgeting and buffered line reads. Stream ownership, flush, safe paths, atomic publication, restart lookup, and trusted manifest loading belong to the file service and are not provided by this codec.

Eleven new Debug cases pass in 66 ms. They cover escaped long Unicode across the 8 KiB input buffer, literal/exact/blank round-trip, missing/extra/truncated/changed rows, nested/number/null values, wrong width/digest, shared-budget exhaustion, a bounded-line failure, cancellation, malformed UTF-8, and rejection of nonempty destinations. No business database or Google request is involved.
