# Retire this local writable instance

Status: Owner-confirmed local retirement and [desktop activation/retirement controls](active-test-store.md) are implemented. Actual device acceptance follows separately.

Retirement preserves the local database and archive. It sets this instance's metadata to Retired, records its reason and source archive, and blocks new business access through the app. It does not delete records, post money, or retire another computer.

## Requirements

1. Sign in as an active owner and provide that owner's passphrase again.
2. Create a complete local archive with the current app. New job records capture the source writable-instance identity; earlier unconfirmed jobs retain their history but cannot authorize retirement.
3. Ensure that archive covers the current business watermark. If another business change occurs, create a new archive first.
4. Provide a retirement reason. Keep the separately saved recovery material and passphrase available for the recovery workflow.

## Verification and recording

`StoreRetirement.RetireAsync` checks the selected completed job's instance, logical store, mode, and exact current watermark. It opens and holds the recorded ciphertext against modification, independently verifies its current size/hash, then enters a writer transaction.

Inside the transaction it rechecks owner access, current instance, archive history, and coverage. It records the Retired state, a distinct retirement audit, and versioned provenance containing owner/time/reason, instance, archive job/hash/manifest hash, and archived/final watermarks. The retirement audit advances the local watermark once. No passphrase is included.

Only after commit does it lock the caller's session. All current-app handles/sessions reject that retired instance through the [instance guard](store-instances.md). The encrypted archive remains byte-for-byte unchanged. Fresh owner access must be established in an explicitly recovered working copy.

Changed/missing archives, old coverage, unknown/different source instances, failed owner confirmation, foreign sessions, and missing reasons block retirement. Later business changes during archive verification block the final transaction too. Cancellation or interruption before commit rolls back the state, audit, receipt, and watermark.

Ten fictional cases cover these boundaries and source preservation. They retire only temporary synthetic stores. Real previous-device retirement and second-device recovery still require their explicit workflow and acceptance checks.
