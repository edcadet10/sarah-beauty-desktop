# Select a recovered TEST working store

Status: Controlled activation and persistent selection are implemented in the business service. Desktop selection/retirement controls and actual device acceptance follow separately.

New recovered copies stay in Prepared state. The public business-store constructor rejects ordinary access to them. Their version-2 provenance retains the original validated manifest for the final checks. Earlier prepared copies remain preserved and require fresh preparation before this activation flow.

## Activation requirements

1. Use a generated recovered-copy request under the TEST data parent's `RestoredStores` directory.
2. Sign in with the passphrase of the owner whose access was reestablished. Prepared-copy attempts retain the normal persisted failure counter and temporary lockout.
3. Retire an accessible previous local primary of the same logical business before activating another copy. On a new root, the previously recorded primary acknowledgement remains part of recovery; this service does not remotely retire another computer.
4. Recheck the prepared identity, schema, original business/watermark, table counts, source hashes, balanced journal totals, managed references, and actual file sizes/hashes. No ordinary business changes may have occurred in the prepared copy.

The internal prepared-store access path is limited to recovery/authentication code. Normal callers open active stores.

## Commit and restart

Activation holds a selection lock, verifies the prepared copy inside a writer reservation, records its Active state and distinct activation audit, and publishes a flushed selection file using same-directory replacement. The selected owner gets a fresh session bound to that instance. Preparation and activation each add their own audit watermark; neither posts money.

The selection file contains relative generated paths and nonsecret identities/time. It contains no passphrase. On restart, resolve it against the TEST parent and require the actual logical/instance/mode identity. Malformed, unsafe, missing, or mismatched selections produce a recovery error; they do not silently open another store.

The file writer uses [.NET file replacement](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0) for an existing selection and a [non-overwriting move](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0) for a new one. Readers and replacement use a separate short filesystem gate. A concurrent-reader test found sharing errors and transient absence with replacement alone; changing share flags was insufficient. Coordinating publication made the forty-replacement probe pass with complete parsed selections.

## Interrupted selection

An ordinary error/cancellation after pointer publication rolls back the database activation and restores the previous selection, provided the selection still belongs to that attempt. A newer external selection is preserved. Failures reported after a committed selection keep that selection and direct the owner to reopen/sign in.

A crash can leave the selection pointing to a copy still in Prepared state. Normal access stays blocked. Fresh activation can complete that existing copy with owner proof. The tests simulate this persisted interrupted state; they do not establish physical power-loss durability.

Selecting an already active current store supports fresh sign-in without resetting its records or rejecting legitimate later business activity. Other stored databases remain preserved.

## Verification

Twenty fictional cases cover local-primary retirement, prepared-access rejection, fresh/new-root activation, later postings/repeated sign-in, changed files/financial facts/counts/provenance, rollback with absent/existing selections, post-commit errors, safe paths/no fallback, interrupted-state completion, external-change preservation, persisted lockout, identity mismatch, concurrent pointer readers, and cancellation after proof without returning an unlocked session. Desktop end-to-end acceptance follows separately.
