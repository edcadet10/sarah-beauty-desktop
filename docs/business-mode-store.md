# Preserve mode across store contexts and documents

The internal existing-store context now carries an explicit TEST/LIVE mode through instance checks, sessions and writer reservations. Public store construction remains TEST-only. Missing existing data is not created, wrong public-mode access is rejected, and a later metadata-mode change invalidates the opened context.

Quote and receipt snapshots now use the context mode. Invoice and credit-note snapshots retain the source quote/invoice mode. Captured readable sources, local generation checks and archive-source headers use the same mode-aware instance fence. The PDF header/footer retain the frozen document tag.

A temporary LIVE fixture initially issued a quote tagged TEST. After correction, 41 store/session/initialization/capture/recovery cases pass, including all four document types, rendered PDF tags, private profiles, balanced posted receipts and complete readable generation in both modes. Copied IDs/credentials cannot reuse sessions across modes. The first wider run had one missing recovery-helper copy; rebuilding test output after the helper was available passed the rerun.

Windows Debug and native TEST profile reopen/lock/close pass, preserving the schema-39/watermark-5 fixture and managed file. LIVE operational examples exist only in temporary fixtures. The app has no public LIVE opening/creation screen yet; clean creation, date/setup gates, mode selection and LIVE recovery/delivery acceptance remain separate work.
