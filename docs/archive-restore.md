# Review a staged archive restore

Status: Isolated decryption, file extraction, database validation, and desktop review are implemented. Activation, restored-instance protection, and second-device acceptance follow separately.

`ArchiveRestores.StageAsync` takes an encrypted archive, an existing private review folder, the separately held protected recovery file, and its passphrase. If a completed backup record is available, pass its `CreatedArchive` facts too. Recovery review does not require the original computer's owner session or credential store.

## Review sequence

1. Hold the encrypted input against modification. Check its bounded size and actual SHA-256. When a backup record is supplied, require its exact ciphertext size/hash before decryption.
2. Decrypt completely into a generated workspace. The helper verifies the encrypted stream through its final authentication data before the package is read.
3. Read the bounded manifest. Require the supported format/schema, TEST or LIVE mode, valid business/store identities, captured watermark, and supported file facts.
4. Reject duplicate ZIP paths, links, absolute/traversal paths, alternate streams, unexpected names, and noncanonical separators/casing. Require exactly the manifest's files.
5. Copy each entry to its generated managed path while limiting actual expanded bytes and checking size/SHA-256. No general directory extraction is used.
6. Open the restored SQLite database read-only. Check integrity, foreign keys, retained source hashes, each posting group's balance, aggregate journal totals, table counts, identity/mode/schema, business name, and watermark.
7. Derive managed PDF/attachment references from that restored database and compare them with the file manifest. Reject extra or omitted references, even if the ZIP and manifest agree with each other.
8. Return a disposable review with the manifest and actual archive/manifest hashes. Keep the working store untouched. Dispose the review to remove its known plaintext working files.

When no backup record is available, the result explicitly reports that it did not match a recorded archive. Internal validation still runs. A public encryption recipient can create another encrypted file; encryption by itself does not identify a specific completed backup.

The implementation treats ZIP names as untrusted because [.NET allows invalid and absolute entry names](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchiveentry.fullname?view=net-10.0). It checks both the [declared uncompressed length](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchiveentry.length?view=net-10.0) and actual streamed byte count. Manifest, file-count, and total-size bounds match [archive creation](archive-package.md).

## What review does not do

Review neither opens an editable business session nor switches the active store. It does not reset owner credentials, reconnect Google, retire another computer, or mark a second-device restore test complete. Activation must revalidate the staged files and apply the separate instance/owner-confirmation rules before allowing writes.

Ordinary cancellation and failures remove only generated known files. An abrupt shutdown or unexpected extra file can leave workspace contents; those contents are not an activated or completed restore.

## Review in the desktop app

1. On the sign-in or storage-error screen, select **Review an encrypted recovery archive**. An active owner session is not required. Alternatively, show a completed attempt in **Backups and recovery** and expand **Decrypt and review this recorded archive**.
2. Choose the encrypted archive if one is not already selected. Choose its separately saved protected recovery file and enter its recovery passphrase.
3. Select **Decrypt and validate archive for review**. Cancel to stop incomplete work.
4. Review the business, mode/schema, captured time/watermark, archive/manifest hashes, journal facts, and paged file/table counts. A review from recorded history must match that exact completed archive. Standalone review explicitly labels the absent local record.
5. Select **Close staged archive review**, leave the view, or return to owner sign-in to dispose its known temporary plaintext. Review clears the passphrase after its attempt and on leaving. It rechecks cancellation after an asynchronous result so a detached view does not retain a late staged workspace.

Fictional native checks validated the same archive from history and before owner sign-in, including two movements/five journal lines, issued PDFs, and all three managed file versions. Wrong recovery proof and cancellation were rejected/cleaned. Closing and returning to sign-in removed the generated plaintext workspace. These checks ran on the development computer and did not activate a restored store.

## Verification

Fictional tests exercise recorded and history-free archive review, exact restored database/files, later active changes, disposal, unsafe ZIP paths, duplicate/link entries, missing files, changed hashes, unsupported metadata, null/oversized manifests, mismatched database facts, omitted/extra references, and an unbalanced journal whose database file hash was updated. Wrong passphrases, changed ciphertext, incorrect recorded facts, and interruption/cancellation at decryption/extraction/database stages preserve active data.
