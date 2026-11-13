# Review packaged period closing and correction history

Date: November 12, 2026. Application: self-contained Windows x64 preview from `9f93851`, schema 40. Fixture: a fresh fictional TEST business with two owners and explicit synthetic statements/evidence. No actual opening, bank review, or expense is attested.

## AC-038 native sequence

The initial closing review blocks unknown cash openings and missing reconciliations. The owner then approves a fictional $1 contribution, explicitly reviews zero openings for Cash/Bank/Processor, imports two manual no-activity statements and one $1 bank statement, matches the contribution, and completes all three reconciliations. Independent checks find one movement/two balanced journal lines and no extra money from openings, matching, or reconciliation.

The owner closes November 1–12. The saved report set has Bank $1 and result $0. An actual approval attempt for a November 10 expense rejects the closed date without posting. Reopening the book period retains the snapshot/reason and still leaves the bank reconciliation's independent protection in place. That same expense still rejects until the owner explicitly reopens Bank. It then posts exactly once, leaving Bank $0 and current period result negative $1.

The revised bank reconciliation explicitly supports the issued $1 expense as outstanding against the retained statement; adjusted statement and ledger balances both equal zero. A new closing saves a distinct revised report set. The original remains reopened and unchanged.

| Retained report | Bank | Period result | History |
| --- | ---: | ---: | --- |
| Original close | $1.00 | $0.00 | Reopened, original hash retained |
| Revised close | $0.00 | negative $1.00 | Closed, separate ID/hash/source watermark |

## Separate reversal safeguards

The first reversal probe targets the bank-matched contribution. Its actual approval rejects the active-match dependency before reaching the closed-date check. The initial harness expected the date message; that interrupted attempt is not proof of that particular guard. Reading the actual error and retrying the same saved draft confirms the active-match protection from AC-039.

After revised closing, a separate backdated reversal targets the expense, which has no active bank match. That actual approval specifically rejects the closed period. Both reversal drafts remain unapproved, and no reversal movement or journal lines are created.

## Independent checks and restart

All original movement/journal/opening/matching/reconciliation/closing rows remain unchanged. Four reconciliation snapshots, two closing snapshots, one book reopening, one Bank reopening, and the full $1 outstanding support retain valid hashes and source links. There are exactly two posted movements/four journal lines with balanced debits/credits of $2. Both closing reports are Ready to close, with no blocking issues, and have distinct increasing source watermarks.

The second owner restarts and opens the distinct closed/reopened report histories. Complete final records, posted/draft movements, accounts, statements, matches, reconciliations, book settings, and reopening rows match the final checkpoint. Integrity and foreign keys pass.

This covers native AC-038, the active-bank-match dependency portion of AC-039, and an explicitly supported outstanding payment. It does not establish the separate unexplained net-zero error scenario, all reversal dependencies, actual statement evidence, physical devices, real Google delivery, signing, or complete release acceptance. No production code changed for this check.
