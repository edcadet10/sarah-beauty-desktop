# Keep unsaved resource-register edits

The resource screen now asks before replacing edited supplier, loan, asset, supply, or actual-trip fields. **Keep editing** retains the current values and selected record; **Discard changes** opens the requested record or new form. Cancelling a register switch also restores the register choice. Navigating to another cached section and back preserves the editor.

Each form provides its existing value reader to a shared draft tracker. Reverting to the original displayed values removes the prompt; invalid entered dates or amounts still count as changes. During a save, a containing control disables the resource screen until the writer completes or fails. The same reviewed values reach the existing storage methods. No schema or posting rules change.

## Validate the behavior

The claim was that replacing a resource editor no longer silently drops its unsaved fields. Losing a field after **Keep editing**, replacing an invalid draft without a prompt, or allowing a competing edit while saving would reject it.

Before the change, the downloaded `96da46d` app silently replaces an edited loan name with a blank form after **New register entry**. The corrected native Debug app passes:

- New and register-reload Keep/Discard checks for all five register types, plus navigation away and back with unsaved text.
- Switching between two saved suppliers: Keep restores the prior selection and private notes; Discard opens the requested supplier without saving those notes.
- A cancelled loan-to-supply register switch, invalid-date preservation, and a reverted-date control that permits a new entry without a prompt.
- A save held behind a real SQLite writer reservation: name, register choice, and New action stay disabled. Releasing the reservation completes one save and restores the editor.
- A second-owner restart reads both saved suppliers and no discarded notes. Independent SQLite checks find exactly revisions 1 and 2, four audits/watermark 4, zero money/journal entries, valid integrity/foreign keys, and unchanged hashes for every table other than the expected Business/Audit/Records updates.

The existing multiline customer/loan fixture also retains its complete saved values, exact CR/LF/CRLF content, original permission entry, and zero money after the unsaved-edit probes. Windows Debug builds with zero warnings/errors. Private fixtures and temporary lock helpers stay outside Git.

This covers the main register fields and observed actions. Separate nested asset-fact and trip-payment review drafts, global lock/shutdown policy, physical touch, and wider accessibility acceptance remain distinct work.
