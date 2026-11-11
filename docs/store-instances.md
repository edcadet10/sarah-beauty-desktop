# Keep writable store instances separate

Status: Persisted instance identity, session binding, stale-handle checks, retired-instance rejection, and [prepared recovered copies](prepare-recovered-store.md) are implemented. Owner-confirmed retirement and active-store switching follow separately.

Schema 32 adds a generated `WritableInstanceId` and Active/Retired state to database metadata. The logical Store ID stays unchanged. Older app versions reject schema 32 when opening the store. The current app checks instance state again during business operations.

`BusinessStore` captures the current instance identity when opened. Each connection checks that identity and active state. Writer transactions check again after obtaining the database writer reservation, before invoking the write delegate. Snapshot reads check inside their read transaction too. Owner sessions carry both logical store and instance identity. Owner sign-in/confirmation cannot reuse a session from another instance or finish a credential proof after its store generation changed.

These checks cover ownerless internal transactions as well as ordinary signed-in writes. They do not renew a human session by themselves. An existing handle whose instance changed must reopen the active store; a retired store rejects new business access. Invalid or missing current-version instance metadata is preserved for recovery review rather than silently regenerated.

## Recovery boundaries

Archive capture checks the snapshot's instance against the authorized source handle. Staged restore validates instance metadata in schema-32 archives. Earlier complete schema-31 archives remain reviewable without migration or metadata changes during read-only review.

Activation must assign a fresh instance identity to the restored working copy, reestablish owner access, and preserve the original store. That activation procedure is separate work. These local checks cannot remotely retire an offline computer. A copied file retains its instance metadata until an explicit activation/reidentification procedure; copying files does not establish a second independent primary ledger.

## Verification

A failing-before case reproduced the session gap: a copied database with a different writable-instance value and the same logical Store ID accepted the original session. The guard makes that case pass while a fresh sign-in still works.

Eleven cases cover restored-session rejection, changed generations and ownerless writes, retirement between opening a connection and reserving the writer, generation changes during credential verification, persisted retirement, invalid/missing metadata without replacement, a preserved pre-upgrade schema-31 snapshot, stable reopening, and read-only review of an older complete archive. Other storage, authentication, posting, backup, and migration cases run in the full suite.
