# Check LIVE setup inside the posting transaction

LIVE money approvals and document issuance now read the retained business profile inside the writer transaction. Missing setup, unfinished formation/EIN/banking/tax reviews, dates before the books start, and dates after today in the business time zone block the operation. These are recorded owner assessments, not verification of registration or tax treatment.

The common posting guard covers the existing financial approval paths. Quote and receipt snapshot creation also check readiness. TEST fixtures keep their existing date behavior. An exact retry of an already-approved receipt returns its original fact even if a later profile review becomes incomplete.

Before the guard, both an unreviewed LIVE receipt and quote succeeded in temporary fixtures. After correction, 42 related cases pass, including permitted reviewed setup, date boundaries, a competing profile change before the writer reservation, and an exact posted retry. Windows Debug passes with no warnings or errors. Native TEST profile reopen, lock and close pass; independent checks preserve its schema 39, watermark 5, three profile revisions and managed file.

The parent full local regression passes 1,430 cases in 7 minutes 1 second. Hosted verification of the earlier mode-session source `8a57f63` passes 1,413 cases in 20 minutes, Windows Debug in 39.85 seconds, and recovery cryptography: [GitHub run](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43245567826). A full regression including these new guards is next.

Public LIVE creation/opening, native selection, LIVE recovery and external acceptance remain separate work. These fixtures do not establish release readiness or supply actual business facts.
