# Read the export column dictionary

The column dictionary lists every column in each business table, each metadata table, and the dictionary itself. Entries preserve table order and one-based column order. They describe literal representation, common money/date/decimal/time/file-size meanings, blank semantics, known join targets, and join scope.

Known targets are checked against the assembled schema. Document and managed-evidence `VersionId` columns resolve separately. Customer and supplier credits, bank and application groups, and tax and retained-book periods also have distinct targets. Polymorphic source/target/value IDs keep their kind-dependent instructions rather than receiving an invented target. Time-zone IDs are configuration identifiers. Booking status history's `From` column is a state, while report-scope `From` is a calendar date.

Join hints do not establish that every historical reference has a current matching row. Keep frozen names/values and original report scopes when reading retained facts. Fields without a reviewed shared type remain described as recorded labels, states, counts, or text; their table column and scope provide context. The dictionary does not guess a more specific type.

The builder verifies all supplied tables and accounts for their headers/rows before adding dictionary cells. Two new Release cases pass for complete ordered coverage, reviewed ambiguous joins/date fields, missing join schema, and combined-budget exhaustion. Metadata/assembly/dictionary checks pass eight targeted cases before the last join additions; the dictionary's two cases pass again afterward. The final generation format, remote verification/publication, and desktop controls remain unfinished. No Google request occurred.
