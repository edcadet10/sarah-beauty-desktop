# Keep completed account dates protected across calendars

Completed reconciliation checks now format their SQL date boundary with the shared Gregorian storage helper. The guard still checks each affected cash account before financial posting, statement import, matching or opening changes.

Six Arabic/Thai public-command probes cover an actual expense approval inside a completed January account period, a valid February approval and an import that introduces a January bank line. Before this change, three failed: Thai settings allowed both covered writes, while Arabic settings rejected the valid February expense. The other three already passed.

After the one-line production change, all 62 related Release cases pass, including reconciliation close/preview, statement import, matching and book periods. Windows Debug builds with zero warnings and errors. Rejected commands preserve the watermark and original financial/import state. No saved data or reconciliation snapshot is rewritten.

Completion/history storage and other banking date boundaries remain separate work. These development-computer checks do not establish full release or actual-device acceptance.
