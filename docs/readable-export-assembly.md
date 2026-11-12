# Assemble readable export table groups

`ReadableExportAssembly.Build` composes all 29 existing projection groups into 155 tables from one captured database. It uses one financial window, a separate planning as-of date, and an event window. Each window spans at most 120 calendar months; the event window starts on or after the planning date. Current register and historical rows remain available where their individual table contracts require them. Selecting a financial range does not filter every raw record table to that range.

Every group must finish before the method returns. A group failure, duplicate table name, mismatched source, failed hash, exceeded budget, or cancellation prevents returning an assembly. Projection and final verification each enforce the same one-million-cell and 64 MiB raw UTF-8 limits. Cancellation is checked between groups and at table-header/row budget operations; it does not interrupt every individual SQLite query.

The result retains source metadata, the selected windows, group-to-table inventory, and frozen verified tables. It makes no human-session renewal and does not alter the business database.

## Verify the assembly

Four new cases and sixteen verifier/calendar cases pass in Release. The combined fixture uses actual approved receipts, applications, an expense, and a confirmed fictional wedding. All 155 tables retain identical serialized contents after later customer edits, owner lock, and Arabic/Thai culture changes. Private customer notes, sensitivities, preferences, and owner credentials remain absent. A deliberately mismatched catalog identity prevents returning even the groups built before it. Invalid windows and cancellation before/during row construction are rejected. An initial test used a nonexistent public customer lookup; it was corrected to the existing authorized search.

This is table assembly, not a completed readable workbook. Start Here metadata, a data dictionary, ownership settings, durable generation artifacts, Google write/readback, publication, scheduling, and UI remain unfinished. No Google request occurred.
