# Discard an unissued client draft

`ClientDraftDiscards.Review` binds the current saved quote, invoice, receipt, or credit-note draft revision and canonical snapshot hash to a private reason. `Discard` requires a current owner session and fresh owner passphrase confirmation. Inside one transaction it rechecks the retained history, unissued state, revision, and hash, saves the discarded record and original snapshot, and records one audit entry. It does not change issued versions, document numbers, financial movements, or journal lines.

Keep the same permanent request ID when retrying the same reviewed request. An exact retry returns the original fact without another audit or watermark change. Reusing that ID for a changed payload, or using another request to discard an already discarded draft, is rejected. Existing save and issue commands cannot revive a discarded draft.

Fifty-four related cases pass across command, retained-history, quote-decision, and invoice-void checks. They cover all four draft kinds, wrong proof, foreign access, stale revisions, changed snapshot content, competing saved edits and issuance, two concurrent discard requests, exact retries, and atomic rollback after injected I/O failure, cancellation, or owner lock. Comparisons retain the original issued versions and complete financial movement/journal content.

The first command/history run passed 29 of 30 cases. Its competing-issuance fixture still referenced an older booking revision, so issuance stopped before competing with discard. Refreshing that fixture to the current booking revision lets the real competing issuance complete; discard then rejects the stale review as intended. No production issuance guard was changed.

The full regression and Windows build are running. A native review screen follows in a separate PR. Actual device/account acceptance and release checks remain pending.
