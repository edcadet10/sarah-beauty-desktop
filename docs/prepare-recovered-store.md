# Prepare a recovered working copy

Status: The business service can prepare an isolated TEST working copy from a validated review. Desktop preparation, active-store switching, explicit local retirement, and second-device acceptance follow separately.

## Required decisions

Provide a current staged review, a new request identity, an existing private destination parent, an active owner from the reviewed archive, a recovery reason, and a new word passphrase for that owner. The caller must explicitly acknowledge that the previous primary is retired or permanently unavailable. The service records that acknowledgement; it does not claim to verify or remotely retire another computer.

Recovery possession authorizes this separate recovery action. The resulting working copy requires fresh owner sign-in. The service does not return an already authenticated business session or change the application's active-store setting.

## Preparation sequence

1. Reread the original manifest from the staged package and require its retained byte hash. Mutable display objects cannot replace that source.
2. Copy each staged file to a new generated workspace, checking actual bytes against the original manifest again. Recheck the copied database, references, and source facts.
3. Apply supported schema migrations to that separate database. Earlier schema-31 archives can become schema-32 working copies; the reviewed database and encrypted archive remain unchanged.
4. Assign a fresh writable-instance identity while retaining the logical Store ID, business identities, historical records, approvals, documents, and file versions.
5. Set the selected active owner's new salted PBKDF2 passphrase and clear that owner's failed-attempt/lockout state. Preserve other owners' credentials. Record a distinct portable-recovery audit action and provenance containing source hashes/watermark, known original instance identity, new instance, selected owner, reason, time, and acknowledgement. No passphrase is included in that provenance.
6. Recheck the resulting identity, source hashes, balanced journals, movement/line counts, and exact debit/credit totals.
7. Publish under `RestoredStores/<request-id>` with a move that preserves existing destinations. Require that the review is still open and cancellation has not occurred at publication.

Preparation advances the copied business watermark once for its recovery audit. It posts no money. Existing databases and the original validated review are preserved. Closing the review after successful preparation does not delete the separate recovered working copy.

## Interrupted or repeated requests

Cancellation and ordinary failures clean the preparation's known workspace without advertising a published copy. Closing the staged review before publication also blocks publication. A request whose destination already exists is rejected with a review action, rather than overwriting or resetting that recovered copy. A destination created concurrently is preserved too.

Activation and retirement controls must complete the one-primary workflow before this copy becomes the application's working store. Existing archives can omit later changes; their exact captured watermark remains visible.

## Verification

Fictional cases check preserved financial JSON/files and source balances, changed selected-owner credentials with other-owner preservation, fresh-instance sessions, provenance without passphrases, independent later postings, repeated/concurrent destinations, changed staged bytes/manifest, mutable display facts, disposed reviews, incomplete decisions, staged failure/cancellation, and older-schema preparation without changing the reviewed database. These checks run on the development computer and do not establish second-device recovery.
