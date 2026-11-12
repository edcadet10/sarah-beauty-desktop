# Project readable business and customer tables

Status: The first explicit readable table group is implemented internally. It is a partial projection, not a complete workbook/export. Other groups, reports, Sheets transport, verified generation publication, and UI follow separately.

All four tables come from one read-only source and retain its generation ID and watermark:

| Table | Included facts | Omitted private fields |
|---|---|---|
| Business | ID, display name, mode, store/instance, schema, watermark, capture time | Unselected settings/identifiers |
| Owners | IDs, display name, active status | Authentication hashes/salts, iterations, lockout details |
| Customers | IDs/revision, name, contact/billing, archive/contact-warning state, creation/update provenance | Makeup preferences, sensitivities, private notes, permission scope/notes, future unselected fields |
| Customer permission status | IDs, photo-status history, date, actor | Scope, notes, media/evidence bytes |

Permission status alone does not describe its scope; review the complete permission record in the desktop app before using photos. The readable group contains no photo binaries.

The shared builder accepts explicit scalar facts only, freezes rows/schema, and hashes deterministic table content bound to its source generation/watermark. Exact Int64/BigInteger money values, fixed decimal quantities, dates, IDs, and booleans become invariant text. Unknown values remain blank, rather than invented zero values. Formula-like text stays literal; the future Sheets writer must use Google's [RAW input option](https://developers.google.com/workspace/sheets/api/reference/rest/v4/ValueInputOption), which does not parse strings as entered values. No CSV or generic JSON/database dump is produced.

Application limits are one million cells, 64 MiB of raw UTF-8 cell text across the shared budget, and 32,768 UTF-16 characters per cell. These are app bounds, not claims about Google's limits or serialized request size. Unsupported nested/binary/floating-point values, malformed schemas/rows, or oversized content fail instead of truncating. Request batching must separately bound actual serialized payloads.

Nine new cases and seven source cases pass (16 targeted cases); Windows Debug has no warnings/errors. They verify private/future-field/authentication exclusion, formula-like names retained as text, exact extreme money/decimal/date values under another culture, row immutability, cell/text bounds, rejected nested/binary/double values, stable hashes/order, and later local customers excluded from an earlier generation. The initial compile needed an explicit collection return type; two fixture assumptions were corrected to account for the seeded customer and permission history. No Google request or complete-export claim occurred.
