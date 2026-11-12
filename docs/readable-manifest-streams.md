# Check a staged manifest before reading tables

The manifest codec accepts at most 1 MiB of JSON with a maximum depth of 16. It rejects duplicate properties, unknown properties, truncated content, unsupported source/format metadata, missing source/groups, invalid table dimensions, inconsistent aggregate counts, invalid journal totals, and a mismatched independently trusted manifest digest.

Header checks run before the file service opens table payloads. Group/table inventory and bounds use the same projection format contract as full generation verification. The writer uses a new empty seekable stream and the canonical manifest serialization. Table payloads must still pass their own bounded readers and complete generation verification afterward.

The new Release fixture covers a valid canonical round-trip and malformed headers, including null objects, duplicate/unknown fields, truncation, oversized input, invalid column counts, missing/unbalanced journal totals, wrong digest, and nonempty output. Null/dimension/journal failures are tested with digests matching the malformed manifests, so they exercise structural validation. Fifteen codec/manifest/generation cases passed before the last journal checks; the header fixture passes again afterward.

This is an I/O boundary, not durable staging or remote publication. Path ownership, flush, non-overwriting publication, restart lookup, and Google delivery remain separate work. No Google request occurred.
