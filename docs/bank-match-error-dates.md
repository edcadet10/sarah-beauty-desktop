# Keep match and bank-error history in its original period

Bank-match/unmatch writes and retained bank-error dates now use the shared Gregorian storage helpers. Carried-error queries compare the same invariant date when choosing prior errors and prior corrections.

All six Arabic/Thai probes failed before the change. Match history returned the wrong year or could not complete its validation. An error reservation appeared before its actual reconciliation date or failed to parse. A March $10.00 correction reduced the unresolved error shown for February from $25.00 to $15.00 under Thai settings; Arabic settings failed to load that history.

After changing four date expressions in two production files, all 67 related Release cases pass. They cover exact match retry without another watermark/group, dated partial unmatch, original error reservations, historical carry-forward, statement dates, reconciliation locks and reopening. Windows Debug builds with zero warnings and errors. Matching and bank-error history create no new financial movement.

The complete local Release regression at `38a69ac` passes all 1,665 cases in 7 minutes 19 seconds, including archive/recovery cryptography. The fresh native bank import, duplicate review, exact matching, unknown-opening block, reviewed fictional zero opening, completion, covered-date rejection, reopening and second-owner restart also pass; see [Review the fresh native release walkthrough](native-release-walkthrough.md).

The first test compilation used two constructor arguments in the wrong order; it was corrected before recording runtime failures. No historical data is rewritten. The source scan finds no remaining simple current-culture `DateOnly.Parse` or `ToString("yyyy-MM-dd")` storage expressions in the Banking folder; this limited scan is not a claim that every date or financial boundary is verified. Other native workflows and device/release acceptance remain separate checks.
