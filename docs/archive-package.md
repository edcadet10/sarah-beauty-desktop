# Create a complete encrypted archive

Status: Local archive creation is implemented in the business service. Desktop recovery setup, archive history, staged restore, cloud upload, and second-device verification follow separately.

`FullArchives.CreateAsync` requires a current owner session, a new output filename in an existing folder, and a valid public hybrid recovery recipient. It needs no recovery passphrase or private key to encrypt. See [Encrypt portable recovery data](archive-encryption.md) for the separate protected recovery material.

## Archive contents

1. Capture a [consistent recovery source](archive-snapshot.md): standalone database, successful issued PDFs, and every managed attachment version.
2. Write `manifest.json` and those exact files into a private ZIP package. Verify each copied file's size and SHA-256 while writing it.
3. Verify the completed package's size/hash and hold it against modification while the helper encrypts it in the standard age format.
4. Independently verify the encrypted output's size/hash, then move it to the selected new destination. Preserve any existing destination, including one created during the operation.
5. Return the captured manifest, encrypted byte count/hash, manifest hash, and public-recipient fingerprint for later backup history.

The manifest identifies the business, TEST/LIVE mode, store, schema, app version, captured watermark/time, files, table counts, and exact journal totals. Its hash covers the actual serialized manifest bytes. The encrypted-file hash covers the completed ciphertext. Keep these facts with the eventual archive job; they are different checks.

Archive creation does not post money or advance the business watermark. Changes committed after its database capture remain outside that archive and must remain pending for a later one. The manifest and temporary plaintext package stay inside the private working directory; no separate plaintext manifest is published beside the encrypted archive.

## Interrupted work

Cancellation, changed source/package bytes, missing files, and interrupted capture/encryption do not publish a completed output. Ordinary cleanup removes only files created by that operation. Unexpected files and existing destinations are preserved. Power loss can leave working files; those files are not completed archives.

The service caps the manifest at 32 MiB, captured files at 100,000, and plaintext package at 64 GiB. Disk-space estimation and user-visible scheduling/retention controls are still pending. There is no automatic pruning.

## Verification

Eleven fictional .NET cases exercise the real helper, decrypt a complete archive, verify every ZIP entry against the manifest, reopen its exact database, and compare counts, watermark, and money. Other cases cover later live changes, interruption/cancellation at capture/package/encryption stages, altered captured bytes and prepared packages, invalid recipients, foreign sessions, and existing destinations. These checks do not establish a completed restore interface or recovery on a second computer.
