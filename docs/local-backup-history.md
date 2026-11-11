# Keep local archive history

Status: Confirmed public recovery settings, manual local archive jobs, persistent history, and coverage checks are implemented in the business service. Desktop controls, scheduling, cloud outputs, and restore activation follow separately.

## Confirm recovery material

1. Create and save a passphrase-protected recovery file separately from the business archives.
2. Read that saved file and confirm its passphrase with `RecoveryProfiles.RegisterAsync`.
3. Retain the protected file and passphrase separately. Earlier archives still need the earlier recovery material after a key change.

The service verifies the exact copied recovery bytes through the helper, then stores only the public recipient, its fingerprint, the protected-file hash, owner, and confirmation time. It stores no passphrase or protected/private identity bytes in these settings. Reconfirming the same recipient/file is idempotent. A changed confirmed configuration is audited and advances the business watermark once.

## Create an archive job

`LocalBackups.CreateAsync` starts a manual job using the confirmed public recipient and a new destination. Its persisted record captures the requested watermark and recipient fingerprint. Successful completion retains the actual [archive result](archive-package.md): captured manifest/watermark, encrypted size/hash, manifest hash, and recipient fingerprint.

Job records use separate versioned operational keys in `AppMetadata`. Recording Running, Succeeded, Failed, or Cancelled does not advance the business watermark. A backup therefore does not make itself perpetually pending. The data snapshot includes operational metadata as it existed at capture; its own job can still be Running in that older snapshot.

Completion records the authorized job result even if the human session has since locked. It does not touch or renew that session. Reviewing history and starting another job require current owner access.

Failures store a fixed code rather than private exception details. Failed/cancelled jobs retain the earlier successful archive record and do not claim their own successful output. If ciphertext was published but history cannot be saved, the service explicitly tells the user to verify that file before retrying. Its unconfirmed history does not count as success.

## Read coverage

`Health` compares the current business watermark with the greatest successfully captured watermark. Later changes remain Pending. A latest failure/cancellation remains visible alongside the last successful coverage and timestamp. Local archive success does not mark a Google Drive upload, readable Sheets export, or second-device restore test complete.

Running means a job is active in this process. An older persisted Running record with no matching active job returns Needs review; the service does not claim that interrupted or external work is still executing. Automatic restart recovery/retry and reasoned resolution of these unconfirmed attempts follow with the worker/desktop controls. No older archive is automatically deleted.

Seven fictional cases cover public-only/idempotent settings, wrong proof and foreign sessions, restart history, exact coverage without a watermark loop, later changes, session locking, failed/cancelled attempts, and published output with interrupted history.
