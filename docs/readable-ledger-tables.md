# Project drafts, approved movements, and journal lines

Status: Explicit account, book-period setting, financial-draft, approved-movement, and journal tables are implemented internally as another partial readable group. Applications/credits, resources, reports, transport/publication, and UI follow separately.

Draft records retain their current revision/status/provenance and remain distinct from approved movements. An incomplete draft keeps its missing date and unresolved tax blank. Posted rows retain their original approved payload revision/hash, actor/time, gross/tax/fee/principal/interest facts, account/party/source IDs, payment reference/classification fields, and linked reversal identity. Their retained payload must match relational identity, date, kind, and amounts. Evidence input text/files and internal cached generations are omitted.

Journal lines retain original movement/account/asset identities and separate exact debit/credit cents. Accounts and closure/opening flags remain separate reference tables. These are read-only projections: they do not post another movement or treat document/application rows as additional cash. Gross movements are not a profit report; loans, contributions, transfers, deposits, and classifications must retain their accounting meaning through the journal and later reports.

Three cases pass: actually approved gross/tax/fee/expense facts and exact balanced sides; incomplete drafts with no posted cash and omitted evidence inputs; and an actual reversal with original identity/opposite bank side. Projection changes neither watermark nor movement counts. Windows Debug is checked separately. The latest full suite has 863 passing cases before billing-link/ledger additions. No Google request or complete-export claim occurred.
