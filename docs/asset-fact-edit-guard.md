# Keep unfinished asset fact reviews

The nested **Reviewed registered asset facts** section now participates in the resource editor's discard prompt. Replacing the asset through New, register reload, or record selection requires a choice when its fact-review cost, basis, reason, or evidence fields have changed. Invalid entered amounts also stay protected.

**Save asset** separately asks before discarding an unfinished fact review. **Keep editing** preserves both the main asset fields and the review. **Discard changes** permits the main asset save; it does not approve the entered fact review or invent cost/tax-basis facts. A successful save reloads that review from the saved asset.

The change adds a small raw-field dirty check, includes it in the existing resource tracker, and reuses a shared confirmation function. The original asset value reader, fact approval, revision checks, and storage methods remain unchanged.

## Check the two replacement paths

The claim was that New and the parent save no longer silently clear an unfinished fact review. Losing either the main fields or review after **Keep editing**, a cancelled save changing storage, or a discarded invalid review reaching approved facts would reject it.

The downloaded `2912a34` app reproduces both losses. An initial harness read ran ahead of the newly attached expander after saving; the completed probe waits for its actual automation button and confirms both outcomes. The corrected native Debug app passes New/Keep/Discard and parent-save Keep/Discard with an invalid review amount, an entered reason, and a changed main asset name. A pristine reloaded review permits New without a false prompt.

An independent complete-table comparison finds no persisted change after the cancelled save. One accepted parent save changes only Business/Audit/Records. The asset advances one revision to 4, with only its name/revision changed in the retained JSON. Cost and tax basis remain null, fact history stays empty, the audit/watermark each advance once to 5, and money/journal counts stay zero. Integrity and foreign keys remain valid. A second owner reads the saved name and unknown facts after restart; every table matches the saved checkpoint exactly.

The common resource confirmation still passes New/reload/Keep/Discard/navigation in suppliers, loans, assets, supplies, and actual trips after this refactor. Windows Debug builds with zero warnings/errors. Private fixtures stay outside Git. Trip-payment review drafts, global lock/shutdown policy, and physical-device acceptance remain separate work.
