# Verify a readable generation manifest

Projection format version 1 contains 29 business groups, 155 business tables, and four metadata/dictionary tables. The manifest binds the source identity, selected windows, group membership, ordered table digests/counts, aggregate row/cell/raw UTF-8 counts, and exact business journal totals. Its SHA-256 digest identifies the manifest contents.

`ReadableExportGeneration.Build` produces this projection from one capture. `Verify` accepts the manifest, table payloads, and an independently trusted expected manifest digest. It checks the supported source/format, freezes and verifies every table, rebuilds the inventory/counts/journal facts, and compares the rebuilt manifest digest. A loader must obtain the trusted expected digest from the job record; it must not treat a digest supplied only by the same artifact as independent verification.

The generation source includes only the selected business identity and capture metadata. It does not serialize the recovery source's database-wide table statistics. Journal totals come from the business-scoped readable journal, use `BigInteger`, and must balance. Aggregated table rows/cells describe the projection and include report copies; they are not counts of distinct transactions.

The table builder/verifier now also accept the minimal generation ID and watermark needed to reload tables without constructing recovery facts or a human session. An independently calculated fixed digest confirms compatibility with the established table JSON format.

Three new cases pass in Release. They cover JSON round-trip/reverification of all 159 tables after owner lock; rejected wrong digest, missing/mixed-source tables, unsupported version, missing group, and altered counts; exact totals of `18446744073709551614`; and rejected unbalanced, fractional, or negative journal values. The group/count failures are checked even with matching digests for the malformed manifests. Thirty related Release cases passed before the final malformed-manifest additions; the three new cases pass again afterward.

This format describes the implemented projection set, including its explicit unimplemented ownership-setting warning. It is not a claim that the app or remote workbook is complete. Inventory/source/schema changes require an intentional format compatibility decision. Durable storage, retry jobs, remote write/readback/publication, and desktop controls remain separate work. No Google request occurred.
