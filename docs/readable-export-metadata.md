# Describe the readable export source and table inventory

Three source-bound metadata tables accompany the assembled business tables:

- **Start Here** identifies the business, TEST/LIVE mode, captured store/instance/schema/watermark/time, assembly version, currency, selected windows, and current group/table counts. It directs corrections to the desktop app and explicitly says that local assembly does not establish remote publication.
- **Export conventions** explains literal cells, exact USD cents and decimal fractions, blank versus zero, dates/instants, scoped IDs, retained versus current reports, planning versus cash, private omissions, file references, and the limits of local digests.
- **Business table inventory** records each business table's group, row/column/cell counts, raw UTF-8 bytes, and exact digest. Metadata tables are excluded from this inventory to avoid circular digests; the final generation manifest must include them separately.

The metadata builder first accounts for all business headers and rows in its supplied budget. Metadata therefore cannot bypass the combined cell/text limits. It rejects a missing group mapping. Two new Debug cases cover all 155 table inventories, stable hashes, source/date selections, combined-budget exhaustion, and a broken inventory.

USD is the agreed currency in [Business rules](business-rules.md). Source database-wide recovery statistics are not copied into the metadata; readable table inventory stays scoped to the selected business projections. Ownership percentages are explicitly marked unimplemented rather than inferred from contributions. The column dictionary, durable generation format, remote verification/publication, and desktop controls remain unfinished. No Google request occurred.
