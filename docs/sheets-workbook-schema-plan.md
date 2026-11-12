# Plan and verify the readable workbook layout

The schema plan binds a verified generation and manifest digest to 159 source tabs and one separate **Export delivery status** tab. Source sheet IDs follow the immutable source-table order; display indexes put **Start Here** first and delivery status second, followed by conventions, inventory, dictionary, and business tables.

Each source tab has exactly its header/data row count and column count. Tabs with data freeze the header; a header-only one-row tab needs no scrolling/frozen rows. All tabs are visible GRID sheets. The operational status tab reserves 32 rows by two columns, adding 64 grid cells without changing source hashes. Its contents and verification/publication behavior remain separate work.

The schema verifier checks workbook ID, exact unique sheet inventory, IDs/titles/order/type/visibility, dimensions, and frozen-row counts. It rejects a changed or extra layout before a generation can be completed. Zero-valued API properties can be omitted. The plan itself does not create or mutate a workbook.

Eleven new Debug cases pass for stable IDs/display order, exact grid sizes/counts, separate status capacity, and rejected missing/extra/changed IDs, names, indexes, types, rows, columns, freezes, and hidden tabs. The design uses Google's [sheet properties](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/sheets#SheetProperties). All responses are controlled fixtures; no Google request occurred. Private creation, schema initialization transport, jobs/worker/status publication, scheduling/UI, and real account/device acceptance remain unfinished.
