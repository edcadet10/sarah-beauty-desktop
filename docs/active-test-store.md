# Select a recovered TEST working store

Status: Controlled activation, persistent selection, and desktop selection/retirement controls are implemented. Actual device acceptance follows separately.

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

Twenty fictional cases cover local-primary retirement, prepared-access rejection, fresh/new-root activation, later postings/repeated sign-in, changed files/financial facts/counts/provenance, rollback with absent/existing selections, post-commit errors, safe paths/no fallback, interrupted-state completion, external-change preservation, persisted lockout, identity mismatch, concurrent pointer readers, and cancellation after proof without returning an unlocked session. Native development-computer evidence is described below; Surface and second-device acceptance remain separate.

## Use the desktop flow

1. Create a current encrypted archive in **Backups and recovery**, review it, and [prepare a separate recovered copy](prepare-recovered-store.md).
2. On the sign-in or recovery screen, select **Activate a prepared recovered copy**. Refresh the catalog and show the selected business/copy before entering its recovered-owner passphrase. Earlier or unavailable copies remain listed with a recovery action.
3. If the previous local primary of that business is still writable, return to it. Show the current complete archive attempt and expand **Retire this local writable instance**. Enter the reason and current owner's passphrase. Review the confirmation, which preserves saved data and explicitly discards unsaved section fields only when retirement proceeds.
4. After retirement, the app shows a recovery screen. Open prepared-copy activation, show the intended copy, and enter the reestablished owner's passphrase. Wait for verification and the newly selected workspace.
5. Restart the app and sign in to the selected copy. Review source records and managed files. A TEST rehearsal on the development computer does not mark a second-device test complete.

The prepared-copy catalog reads known generated folders without modifying their databases. Activation and retirement run outside the UI thread, support cancellation, and clear passphrase fields when completed or unloaded. Startup resolves the stored selection before opening the business; locking/reloading the workspace uses that same resolver.

A complete fictional native rehearsal blocked a competing local primary, respected a cancelled retirement confirmation, retired only the isolated source fixture after verification, rejected wrong recovered-owner proof, activated the copy, and reopened it after restart. Pinned receipt v1 verified; January income/expense/result/bank remained $0/$10/-$10/-$10. Read-only comparisons found exact retained financial/document/file/link source rows, two movements, five balanced journal lines, three managed versions and hashes, and only the expected retirement versus recovery/activation audit watermarks. The earlier rehearsal store stayed active and unchanged. The initial script tried entering a secret before the cancelled dialog's continuation had reenabled its fields; waiting for enabled state corrected that automation timing.
