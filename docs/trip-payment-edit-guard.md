# Keep unfinished trip payment reviews

In the downloaded preview at `d1fbb426dcf96244f647ad3cc496afc762d703f8`, New register entry and Load trip reimbursement review silently replace unfinished payment-link fields. Reviewing original payment detail also clears unfinished release fields. Native probes reproduce all three losses without submitting a financial decision.

## Confirm before replacing a draft

Trip link and release forms now compare their raw fields with the values initially displayed. Invalid amounts remain unfinished changes. New entry, register/record changes, parent-trip saving, review reload, and replacement of original-payment detail use the existing Keep editing / Discard changes dialog. Cached section navigation retains the form. A review reload disables its nested controls until loading finishes.

Successful link/release approval refreshes its completed review. If that refresh would replace an unfinished draft in the other form, confirmation happens before submitting the approval. Keep editing preserves both drafts and submits neither decision. Existing saved-trip checks, captured-field checks, owner approval, financial rules, and persistence remain unchanged.

## Check the replacement boundary

The claim is that the guarded actions preserve unfinished trip-payment fields unless the owner explicitly accepts their replacement. Losing a field after Keep editing, accepting invalid input as clean, or changing saved history during a cancelled action rejects the claim.

- Original downloaded-app probes reproduce each loss. Fixed native probes pass New/reload/parent-save Keep, invalid link input, cached navigation, explicit reload/New Discard, and a clean reverted form.
- Original-detail/reload/New/parent-save Keep and explicit original-detail Discard also pass with invalid release input. Both approval paths keep the other unfinished draft when cancellation is chosen.
- A separate fresh TEST business approves one actual $21.50 expense, links $10, and releases $5 of attribution. Both successful approval paths refresh without asking to discard their own completed draft. Independent rows contain one movement, two journal lines balanced at 2,150 cents per side, one 1,000-cent link, and one 500-cent release. Attribution creates no additional payment or expense.
- Existing fictional saved tables remain exact in the fresh-field fixture. An older release-history fixture upgrades to schema 40 when first opened: its pre-existing business tables remain exact, while schema metadata and eight empty tables change. Repeating the original/fixed probes from a separate post-upgrade checkpoint preserves every table exactly; the initial all-table comparison is not counted as a pass.

The Windows Debug build passes with zero warnings/errors, and the self-contained Windows x64 publish succeeds. The packaged app repeats the link, release, and other-draft approval-cancellation probes. A second owner reopens the fresh approved fixture and reads its $5 net attribution. All saved tables in all three fixtures match their post-upgrade/post-approval checkpoints, with valid integrity and foreign keys. This correction does not establish a universal lock/shutdown policy or physical Surface operation.
