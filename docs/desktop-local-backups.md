# Create an archive from the desktop

Status: The Backups and recovery workspace supports protected recovery-file setup, manual local encrypted archives, persistent history, and ciphertext verification. Scheduling, Google delivery, restore activation, and second-device acceptance follow separately.

## Save and confirm recovery material

1. Open **Backups and recovery**, then expand **Portable recovery setup**.
2. Enter an easy-to-type word passphrase with 15–256 characters and repeat it.
3. Select **Create and save recovery file**. Choose a new `.sbrecovery` file with the Windows Save dialog.
4. Wait for the recipient fingerprint and confirmed setup message. The app rereads the saved bytes and verifies the passphrase before registering the public recipient.
5. Keep the protected file separately from archives, and retain its passphrase separately. Retain earlier recovery material for earlier archives after changing keys.

The extension identifies a protected recovery file; its contents use the standard age format. The app does not save the passphrase or protected/private identity bytes in its recovery settings. It clears passphrase fields after each setup attempt and when the setup view unloads.

To reuse a saved file, enter its passphrase and select **Confirm existing recovery file**. The Open dialog accepts `.sbrecovery`, `.age`, `.txt`, or `.agekey`. Wrong material/passphrases return a fixed error. Reconfirming the same file is idempotent.

Existing destinations are preserved. If saving completed but confirmation failed, the message directs you to confirm that saved file rather than replace it.

## Create and verify an archive

1. Select **Create encrypted archive now**.
2. Choose a new `.age` destination. Existing destinations remain unchanged even if the Windows dialog offered an overwrite prompt.
3. Wait for the completed captured watermark, byte count, and ciphertext SHA-256. **Cancel local archive** or leaving the section cancels incomplete work.
4. Review the selected attempt's requested/captured watermark, timestamps, destination, recipient, ciphertext/manifest hashes, journal facts, and paged file manifest.
5. Select **Verify selected encrypted file** to compare that file's actual size/hash with its completed record.

Checksum verification is separate from decrypting and validating a restore. A local copy does not protect against losing this computer; keep a copy on another device or private storage.

## Read backup health

**Refresh backup health** loads saved recovery confirmation, attempts, and current watermark coverage. A later failed attempt stays visible alongside the last successful coverage. Changes after capture remain pending. History survives restarting the app.

The dashboard reads this local coverage separately from its financial capture and links back to the workspace. Drive upload, Sheets export, and second-device restore test retain separate Never completed labels until those operations exist and complete.

The picker follows Microsoft's [Windows App SDK save-file guidance](https://learn.microsoft.com/en-us/windows/apps/develop/files/pickers-save-file). The app writes and verifies its own new output after selection.

## Verified on the development computer

Fictional native checks saved and confirmed protected material, validated field clearing, created/verified an archive with its file manifest, and retained history/dashboard coverage after restart. Wrong recovery proof was rejected; the saved file reconfirmed with the correct phrase. Changed ciphertext was rejected and its restored original verified again. An attempted overwrite preserved the earlier archive byte-for-byte, recorded failure, and retained earlier success.

The latest full suite has 584 passing .NET cases and four separately passing Go groups. The Windows Debug build has no warnings/errors. These checks do not replace Surface interaction, offline release acceptance, or a restore on another Windows computer.
