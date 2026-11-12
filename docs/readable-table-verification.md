# Verify readable table content before assembly

`ReadableTableVerification.CopyAll` accepts tables from one captured generation and watermark. It rebuilds each table through the existing bounded scalar builder and compares the resulting SHA-256 digest with the supplied digest. Altered names, headers, values, row order, row widths, and missing rows cannot retain the original digest.

The verifier rejects an empty set, duplicate names without regard to case, more than 256 tables, and content beyond the shared cell/text budget. Successful results contain frozen copies of the schema and rows, so changing mutable input collections cannot change the verified result. Exact integer and formula-like values remain literal text.

Ten new cases pass alongside nine existing builder cases in Debug. They exercise source/content corruption, mutable aliases, empty/duplicate sets, and cell/byte bounds with fictional data. The verifier establishes content integrity only: it does not yet establish that every required export group is present, that a workbook exists, or that Google has read back any data. Assembly, dictionary, delivery, and publication checks remain separate work.
