# Prepare storage for dated owner interests

Schema 33 adds append-only ownership version and share tables. Each business has its own revision sequence. Versions retain an effective date, recording owner/time, immutable snapshot/hash, optional evidence version, and correction link. Shares retain owner identity, the name at confirmation, and exact decimal fraction text. Business-scoped foreign keys reject another business's owner or version. Unique indexes prevent duplicate initial dates and competing corrections.

Database triggers reject updates and deletes for confirmations and shares. All four ownership-schema Release cases pass after adding the triggers, including direct attempts to change or delete both record types while preserving their original values.

The upgrade creates no percentages or ownership confirmations. Login status, actual contributions, drawings, reimbursements, and loans do not determine ownership. This storage step adds no allocations, financial postings, tax basis, settings service, or desktop form. The service must validate complete shares totaling 100%, calendar dates, evidence scope, corrections, and stale revisions before recording a confirmation.

The writer schema increases so older app versions cannot silently ignore the new contract. Existing schema-32 stores receive the normal verified pre-upgrade snapshot. Unsupported existing tables fail the transaction rather than being replaced. Existing Google export jobs retain their recorded schema; older source artifacts must not be presented as current-schema coverage.

All 57 targeted Release cases pass across ownership migration, database initialization, instance identity, and recovered stores. New cases preserve an actual posted receipt, journal balance, store/instance identity, watermark, and an openable version-32 snapshot; repeat opening creates no second snapshot. A malformed pre-existing table leaves both its original row and schema version unchanged. Cross-business recording-owner and share inserts fail. These structural tests do not establish percentage validation or completed ownership settings.
