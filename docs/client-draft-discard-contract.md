# Review an unissued client draft before discarding it

The draft-discard contract identifies one saved quote, invoice, receipt or credit-note draft by kind, permanent identity, revision and canonical SHA-256 snapshot digest. It requires a bounded private reason. Existing opaque draft identities are preserved exactly rather than converted into document-version GUIDs.

Nineteen contract cases pass across all four kinds, invalid/unsaved revisions, missing identities, malformed digests and private-reason bounds/control characters/Unicode. Windows Debug passes with zero warnings/errors. This increment creates no discard records or status changes; retained storage, commands and native review follow separately.

Discarding an unissued draft must not void an issued document, alter source coverage, allocate a document number, remove a retained PDF or change posted money. Those command-level boundaries remain to be verified when the command is implemented.

The corrected full credential-gate regression passes 1,515 cases in 6 minutes 54 seconds at `026f142`. Hosted verification of that fix is running. Earlier workspace-selection source `eaf43ea` passed 1,514 hosted cases, Windows Debug and cryptography. Complete workflow and release/device/account acceptance remain pending.
