# Verify entered and effective literal cell values

Readable verification checks both `userEnteredValue` and `effectiveValue`. Nonblank cells must contain exactly the expected string in both fields. Formulas, numbers, booleans, errors, ambiguous union members, duplicate/unexpected fields, changed text, and missing nonblank values are rejected. Omitted cells or empty value objects are accepted only for expected blanks; an explicit JSON null cell is malformed.

The distinction follows Google's [CellData fields](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/cells): effective values can be calculated results, while entered values retain the stored kind. [ExtendedValue](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/other#ExtendedValue) distinguishes strings from formulas and numeric values. Future grid reads must request both value fields through an explicit field mask; this verifier accepts that limited cell shape.

Seventeen new cases pass in Release; these plus the request planner pass 21 targeted cases. A deliberate formula fixture has exactly the expected calculated text but fails literal verification, demonstrating why displayed/effective equality alone is insufficient. Other cases cover formula-like literal text, Int64 maximum text, zero, decimals, dates, Unicode/newlines, blanks, coercions, unknown/duplicate fields, and fixed error messages without cell contents.

These are controlled JSON fixtures, not real Google delivery. Range identity/offset/coverage, bounded response planning, workbook schema, permission/source fences, completion publication, jobs, and UI remain separate work. No HTTP request occurred.
