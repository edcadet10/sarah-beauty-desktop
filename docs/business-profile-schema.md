# Prepare append-only private profile history

Schema 39 adds an initially empty `BusinessProfileVersions` table. It binds the recording owner to the same business, makes revision and retry identity unique within that business, and rejects updates/deletes. Typed snapshots and payload hashes are stored privately. The migration invents no profile, review state, EIN, ownership, start date, cash balance or LIVE authority.

Private profile facts and review reasons are explicitly excluded from readable tables. Full encrypted archives retain the database. When preparing an older archive, only the known empty new profile table may appear as a migration addition; unexpected tables or invented rows still prevent activation.

Sixteen initial schema/ownership/recovery cases and 20 expanded upgrade/contract/workbook cases pass. Checks cover an openable schema-38 snapshot, unchanged issued identity/watermark, owner scope, immutable history, unique revision/request, unsupported earlier layout rollback, and actual encrypted older-schema restore/activation. Legacy fixtures remove only empty newer tables and retain the real layout through schema 38. Typed private-history verification, authenticated saves and native controls follow separately.

The complete local Release regression through this storage step passes all 1,373 tests in 7m52, including archive/recovery cryptography. The later typed-history and authenticated-save increments have separate validation.
