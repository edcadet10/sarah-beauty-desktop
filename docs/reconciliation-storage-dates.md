# Preserve reconciliation dates and reopening order

Reconciliation completion, history, continuity, opening/correction sources and reopening boundaries now use the shared Gregorian storage-date helpers. Saved snapshot JSON and its content hash remain unchanged.

Eight Arabic/Thai probes failed before the change. Valid consecutive no-activity completions were blocked; existing history threw or returned different dates; Arabic settings blocked the latest reopening; Thai settings allowed an earlier reopening despite a later completed dependency. Historical sources also lost their confirmed opening or included a later correction choice. The later correction did not change the independently filtered historical ledger balance.

All 61 related Release cases now pass, including the eight new cases, reconciliation date locks, close/preview, retained statement errors, period close and readable reconciliation projection. Windows Debug builds with zero warnings and errors. The tests preserve both original snapshot hashes through reviewed reopening and verify that rejected reopening changes no watermark.

The first test compilation used an incorrect source-property name; it was corrected before recording the eight runtime failures. This change does not rewrite historical data. Bank-match writes and carried-error date boundaries remain separate work, alongside full native/device/release acceptance.
