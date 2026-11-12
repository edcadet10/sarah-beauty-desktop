# Read every source range and the delivery status

The workbook reader checks full private job bindings and exact schema before reading every planned source range. Each range requires the expected workbook, sheet, offsets, shape, and matching entered/effective literal strings. It then rereads schema and privacy. The separate status reader checks the fixed status layout, literal cells, and private identity. Both staging and verified states support read-only inspection.

These observations are sequential. They do not provide an atomic snapshot of a remotely editable workbook or prevent later edits/sharing. The caller still checks current local permission and the actual token connection, serializes delivery, and performs the final publication and completion checks.

## Check complete coverage and late changes

The initial tests disproved the assumption that each tab needs one read request: the fictional fixture uses 168 source ranges for 159 source tables, plus one status range. Coverage now counts every planned range and all 160 distinct tabs. The test originally failed four count assertions; no production reader change was needed.

The seven cases cover complete staging/verified readback using GET only, a formula in the final source range even when its effective text remains expected, schema or privacy changes after all source reads, withdrawn access, and mismatched source before HTTP. Controlled JSON responses do not establish real Google acceptance. The complete worker, pacing, scheduling, and controls remain subsequent work.
