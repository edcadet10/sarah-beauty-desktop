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

The self-contained publish succeeds with 575 files totaling 313,832,410 bytes. Its second-owner native run repeats all five New/reload/Keep/Discard/navigation cases. A complete per-table comparison preserves every saved table exactly after both restart and those published-app probes.

GitHub [preview run 43371951319](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43371951319) succeeds for immutable source `2912a34be0e9889723877164aef3a9eaf70371a3`. All 575 downloaded paths, byte counts, and SHA-256 hashes match the manifest, with no rejected business-data file types. The downloaded app repeats the five-register probe for the second owner and preserves every saved table exactly. The unsigned artifact expires November 27, 2026.

This covers the main register fields and observed actions. Separate nested asset-fact and trip-payment review drafts, global lock/shutdown policy, physical touch, and wider accessibility acceptance remain distinct work.
