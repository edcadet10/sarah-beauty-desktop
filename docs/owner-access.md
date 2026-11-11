# Confirm owner access

Passphrase verification runs before the short database transaction that records its result. The PBKDF2-SHA256 settings remain 600,000 iterations with independent salts. Expensive verification does not reserve the business database writer.

Before accepting the result, the transaction rereads the active owner, business, credential material, current failed-attempt count, and lockout. Changed credentials or a deactivated owner invalidate an earlier proof. A lockout recorded while verification runs also blocks that proof. Concurrent failures increment the current persisted counter instead of replacing it with an older count.

Successful confirmation still identifies the authenticated owner. Failed sign-in and temporary lockout behavior remain persisted. Password change/recovery, automatic desktop idle locking, and restore-session invalidation remain separate pending work.

## Verification

GitHub exposed a database-lock timeout in the competing supplier-credit approval test. A deliberate local gate reproduced the problem: pausing passphrase verification while its write transaction was open prevented an unrelated customer save and produced SQLite Error 5. The same test passes after moving verification outside that transaction.

Five new cases cover writer availability, changed credentials/deactivation, concurrent failure counters, and a lockout recorded during a valid proof. Existing owner-access and competing supplier-credit tests also pass. The full main-based suite has 506 passing cases, and the Windows Debug build has no warnings or errors.
