# Backup and Recovery

Status: Recovery contract for the planned desktop application. No desktop backup or restore has been implemented or verified yet.

## Two separate outputs

| Output | Purpose | Contains | Does not establish |
|---|---|---|---|
| Private Google Sheets export | Readable business records and reports | Export timestamp/watermark, IDs, core customers/bookings/documents, financial records, balances and reports | Complete application recovery or a second editable ledger |
| Encrypted archive in private Google Drive | Restore the desktop business store | Consistent SQLite snapshot, all referenced local receipt/photo/PDF files, nonsecret settings, schema/app version, manifest and hashes | A successful restore until recovery has actually been tested |

The readable workbook excludes authentication material, recovery keys, private sensitivities/notes, and photo binaries. These private records remain in the encrypted full archive. Authorized spreadsheet editors can read the exported records; it is not protected by the archive's encryption.

Both destinations must remain private to the intended owners. A public link is not a backup requirement. Keep Google file IDs and account references in private settings rather than repository examples.

## Snapshot and archive procedure

1. Record business identity, mode, app/schema versions, and the committed-data watermark.
2. Create a consistent database snapshot using SQLite's backup API or another verified SQLite snapshot mechanism. Do not copy only an actively changing `.db` file while ignoring its WAL state. See [SQLite backup documentation](https://www.sqlite.org/backup.html).
3. Read the attachment list from that snapshot and pin those immutable file versions against cleanup. Verify each exists and has the recorded hash/size.
4. Include the snapshot, referenced attachments, nonsecret settings, and a versioned manifest. Paths must be relative and remain inside the restored data directory.
5. Encrypt the archive using a maintained library and authenticated encryption, with a portable recovery mechanism. Choose the library, format, key derivation, and owner recovery procedure before implementation; do not invent a cryptographic format.
6. Upload as a new archive version. Verify remote completion, expected size, and the applicable remote checksum or download/hash comparison before marking success.
7. Save the result locally, release pinned files, and apply the confirmed retention policy only after a newer complete backup exists.

Failed/incomplete archives never replace the last known complete archive. Tokens and device-bound credentials are not backed up as portable secrets. Owners reconnect Google and reestablish device-specific authentication after restoring.

## Readable export contract

Build each export from one completed database snapshot. Use a generation ID and matching data watermark across all exported tables/reports. Data dictionaries and stable IDs must make joins understandable.

Export groups:

- Start Here: source, export time, mode, schema version, data watermark, completeness, setup warnings.
- Customers: names/contact/billing and IDs, excluding private notes/sensitivities.
- Catalog and bookings: services/components, events, charges, places, appointments, artists, payer responsibility.
- Documents: issued-version summaries and lines, payment plans/shares, amounts due, document-file references.
- Money: posted transactions, applications, journal entries/lines, credits/refunds, relevant approval metadata.
- Purchases and resources: suppliers, purchases/lines, supplies, assets/movements, mileage.
- Owners and debt: owner movements, ownership settings, loans/movements.
- Banking and tax: account summaries, statement/match/reconciliation records, tax components/periods/remittances.
- Reports: profit and loss, balance sheet, trial balance, cash, customer/supplier balances, owners, assets, debt, tax, and dashboard datasets.

Write into a staging generation, verify expected table counts/totals and API results, then publish its completed-generation reference. A partial write must not advertise a mixed old/new export as current. Preserve the last complete readable generation until a new one is verified.

Edits in Google Sheets are not imported. Label it "Export from Sarah Beauty Desktop" with its as-of time and direct owners to correct data in the application. Existing workbook/forms migration needs a deliberate cutover; never leave two systems posting into the same books.

## Scheduling, failures, and cost

Queue backup work after committed changes. Coalesce repeated changes and retry with bounded exponential backoff while the app is open and connected. Preserve pending state across app restarts. Offer Back up now and Export now independently.

Show separate states: Never completed, Pending, Running, Succeeded with timestamp/watermark, and Failed with a useful next action. Changes after a successful snapshot remain pending even if that snapshot finishes uploading later. Don't reset both success timestamps when only one output succeeds.

Handle expired/revoked sign-in, denied file access, offline operation, quota limits, full Drive storage, local disk exhaustion, and interrupted uploads. Stop/retry cloud work without blocking ordinary local records. Confirm local failure visibly; never claim an unsaved record was saved.

Standard Google Sheets/Drive API use is currently described as available at no additional cost, with future charges for exceeding quota limits planned later in 2026. This is not an unlimited-free guarantee. Keep the project without billing enrollment, stay within available quotas/storage, and recheck terms at implementation. Sources checked September 21, 2026: [Sheets limits](https://developers.google.com/workspace/sheets/api/limits), [Drive limits](https://developers.google.com/workspace/drive/api/guides/limits).

Backup interval and retention count/age are still open choices. Show estimated archive size and available space before enabling a schedule. If storage fills, report the problem; do not silently delete old recoverable history or purchase storage.

## Restore procedure

1. Select a complete backup, confirm its business/mode/date, and obtain the separately held recovery material.
2. Decrypt and verify integrity before touching current live files. Reject unsafe paths, unsupported schema versions, missing attachments, or wrong keys.
3. Restore into a new isolated directory and run SQLite integrity/foreign-key checks, attachment hashes, journal-balance checks, and source/target balance checks.
4. Compare record counts, key report totals, and sample documents/photos with the manifest and known snapshot evidence.
5. Open the restored store in review mode, preserve its historical IDs and approvals, and reestablish local owner access. Reconnect Google separately.
6. Preserve the existing working store. Require an explicit owner confirmation before replacing the active store; record the restore and its provenance.
7. Retire the previous writable instance before continuing, so restoring onto a second computer does not create competing primary books.

Test recovery on another Windows computer with no access to the original device's encrypted credential store. A key encrypted only for the original Windows profile is not sufficient for disaster recovery. Test a wrong key, corrupted archive, interrupted restore, and missing attachment. Record the last successful restore test separately from upload success.

Recovery can lose changes after the last successful archive; display that archive's exact watermark/date. No zero-data-loss promise is made for work that has not yet been backed up.
