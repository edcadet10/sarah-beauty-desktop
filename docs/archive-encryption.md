# Encrypt portable recovery data

Status: The encryption helper, desktop process bridge, reference interoperability checks, and [complete local archive creation](archive-package.md) are implemented. Staged restore validation and the desktop recovery interface follow separately.

The selected format is [age v1](https://c2sp.org/age), using the maintained [age 1.3.2 Go library](https://pkg.go.dev/filippo.io/age@v1.3.2). New business recovery keys use its native hybrid ML-KEM-768/X25519 identity. The app uses the library's encryption functions and format directly.

## Recovery material

1. The owner chooses a word-based recovery passphrase with 15–256 characters.
2. The helper generates a recovery identity in memory. It returns the public recipient and a standard age-encrypted identity file, protected with the passphrase. It returns no plaintext private key.
3. The owner will save that protected recovery file separately from the business archive and retain the passphrase separately. The desktop setup interface is pending.
4. Automatic archive encryption uses the public recipient. It needs no saved recovery passphrase or plaintext private key.
5. Restoring uses the protected recovery file and its passphrase. The helper unlocks the identity in memory, decrypts the archive stream completely, and checks its final authentication data before reporting success.

Recovery-file protection uses age 1.3.2's [scrypt policy](https://github.com/FiloSottile/age/blob/v1.3.2/scrypt.go): N=2^18, r=8, p=1. The helper rejects higher work factors when reading recovery material. Passphrases are not normalized. The recovery file uses the standard age format; no device-bound Windows secret is required to decrypt it.

## Process and file boundaries

The Windows helper accepts one bounded, versioned JSON request on standard input. Passphrases and protected recovery material are passed through that local pipe, rather than command-line arguments. Responses contain fixed error codes, public recipient information, protected recovery bytes, or output size/hash. Errors do not echo private inputs or filesystem paths.

Encryption/decryption creates a new output file exclusively and preserves an existing destination. Normal failures remove the helper's incomplete output. The caller must keep output in an isolated staging directory until its own archive verification and publication finish; termination can leave an incomplete staging file. Streaming limits cap plaintext at 64 GiB and allow standard format overhead on ciphertext input.

The .NET bridge keeps each worker's output in its own temporary sibling directory. It waits for the worker, checks the actual file size/SHA-256 independently, then publishes with an operation that preserves any existing destination. Cancellation and deadlines terminate and drain the child process before cleaning its known staging output. Requests, responses, and error output are bounded; malformed responses produce fixed safe errors. The Windows helper and notices are copied with the business/desktop build output.

Age's authenticated chunks check encrypted-file integrity. The archive layer must also check the selected backup's recorded hash when available, business/mode, manifest, referenced files, database, and ledger before activating a restored store. Encryption success alone does not establish a tested restore.

## Build and verify

Use Go 1.27.4 and the committed module checksums. From the repository root:

```powershell
./tools/build-recovery.ps1
```

The helper and dependency notices appear under `.artifacts/recovery-helper`. This remains a build artifact, while source and dependency definitions are saved on GitHub.

To run the crypto checks, build the official reference CLI and set its location:

```powershell
go install filippo.io/age/cmd/age@v1.3.2
$env:SARAHBEAUTY_AGE_CLI=Join-Path (go env GOPATH) 'bin/age.exe'
Push-Location tools/SarahBeauty.Recovery
go test -count=1 -v ./...
Pop-Location
```

Four test groups cover protected recovery, empty/exact-chunk/multiple-chunk streams, independent output hashes, wrong passphrase/identity, changed/truncated ciphertext with incomplete-output cleanup, existing-destination preservation, reference CLI interoperability in both directions, bounded protocol inputs, fixed safe errors, and excessive recovery work. CI runs them as a separate Windows job. Tests use temporary fictional data and never use an owner's real recovery material.

Five .NET cases also run the real helper through its local pipe. They cover generated/inspected recovery material, multiple-chunk encryption/decryption and independent hashes, wrong passphrase/truncation without plaintext publication or private-input errors, cancellation/timeouts with cleanup, a destination created by another operation, wrong output hashes, oversized recovery input, and a missing-helper action. Archive creation has separate end-to-end and failure checks.
