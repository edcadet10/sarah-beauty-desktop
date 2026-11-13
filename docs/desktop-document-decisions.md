# Review and record a document decision

1. Open **Document decisions**. Search for the exact saved quote or invoice version, then select **Review selected document**.
2. Check its issued snapshot, number, version identity and original PDF. Review any retained decision and supporting file.
3. For an issued quote, choose decline, withdrawal or void. For an unused invoice, choose void. Enter an ISO date and a private reason. Optionally select an exact managed receipt or evidence file.
4. Select **Review and record document decision**. Review the frozen action, date, reason and file, then enter your own passphrase in the private confirmation dialog.
5. Select **Record decision**. The page reloads the retained decision and shows its terminal status. The original issued document stays unchanged.

Accepted agreements require their cancellation workflow. Invoices with payment or credit-note history require linked adjustments. The service rechecks current state, dependencies, evidence and authorization before writing; the screen's loaded state cannot authorize a stale transition.

Cancelling confirmation preserves the draft. Replacing or refreshing an edited draft requires explicit discard. Navigation preserves date/reason/file choices and clears confirmation phrases. Foreground work is cancellable and registered for shutdown; leaving the page cancels it. After cancellation during a write, refresh retained history before retrying. The same reviewed payload reuses its permanent request identity.

Windows Debug builds with zero warnings/errors. A fictional native walkthrough passes ISO-date rejection, private phrase controls, confirmation cancellation, reviewed discard, navigation preservation, wrong-phrase rejection, all three quote decisions, unused-invoice void with managed evidence, paid-invoice rejection, accepted-agreement exclusion, restart and owner lock. Read-only checks retain seven issued versions, four unique decisions/audits at watermark 34, one original $54.50 receipt/application and balanced journal, two evidence pins, and verified managed/PDF hashes. No real client message, Google request, LIVE entry, or device acceptance occurred.
