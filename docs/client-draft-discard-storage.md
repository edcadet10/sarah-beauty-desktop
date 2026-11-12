# Retain type-bound client draft discard history

Schema 40 adds the private append-only `ClientDraftDiscards` table. Each row binds a business, source draft kind/record kind, revision, permanent request, recording owner and hashed JSON. Foreign keys bind the source to the same business and exact record type; owner scope, one discard per draft and unique business/request identities are enforced. Updates and deletes are rejected.

Upgrades preserve a verified pre-migration snapshot and create no discard facts. Older archive preparation permits this known new table only when empty. Earlier-layout test helpers now remove only verified-empty newer tables, preserve profiles in schema-39 archives and refuse to erase retained discard rows. Readable export conventions explicitly omit private discard reasons/source snapshots.

Forty-one related schema/upgrade/metadata/mode-recovery cases pass. They cover all four draft kinds, wrong type/business/owner rejection, uniqueness/immutability, unsupported earlier-table preservation, existing money/profile/invoice identity and schema-31/35/38/39 archive recovery. The first run had one old test expecting current schema 39; it now checks the declared current version, with faithful older-layout fixtures.

Windows Debug passes with zero warnings/errors. A native TEST profile fixture upgraded from 39 to 40, reopened, locked and closed normally. Independent read-only checks retain watermark 5, three private profile revisions and the managed file, with no new discard/financial/document/ownership/Google facts. Its single verified prior snapshot remains schema 39 with watermark 5 and all three profiles.

This increment provides storage only. Typed retained-history validation, transactional discard commands and the native workflow follow separately. The parent credential correction passes 1,515 local and hosted cases; full schema-40 regression follows. Release and actual device/account acceptance remain pending.
