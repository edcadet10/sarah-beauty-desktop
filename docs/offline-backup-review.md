# Put local backups first

Date: November 12, 2026. UI source: `0a6185b`. Schema remains 40.

Backups and recovery now presents recovery-file setup, automatic local archives, backup health, a primary emerald **Create encrypted archive now** action, history, and file verification before optional online copies. The collapsed **Optional Google copies (internet required)** group explains that local backup/recovery needs no Google setup. The primary status uses a local completion date; fingerprints and internal coverage values remain under **Backup verification details**.

## Native layout checks

The claim was that local backup is reachable before any Google setup. The falsifying observation would be a missing/unreachable local action or online controls appearing first. Native UI Automation confirms local action order, collapsed online options, and a visible action at least 44 pixels high at 1280- and 500-pixel window widths. Both rendered captures were inspected. Debug builds with zero warnings/errors. These are scoped layout checks, not complete touch, keyboard, or accessibility acceptance.

## Backup and review with network access blocked

Two temporary Windows Firewall outbound-block rules apply only to the exact packaged and Debug test executables. The firewall service and all profiles are enabled; the rules' active program filters match those executables. The user's browser and other programs remain connected. Both owned rules are removed after the checks.

Using the packaged app at `8dda1a7`, a fictional owner creates a portable protected recovery file, then a complete local encrypted archive, through native file dialogs. The second owner restarts, verifies the recorded archive hash, gets a clear rejection for an incorrect recovery passphrase, then decrypts and validates the same archive with the correct fictional phrase. Closing the staged review leaves the working business unchanged. No Google configuration, grant, or output authority exists in this fixture.

The new Debug layout repeats second-owner file verification, wrong/correct recovery checks, and staged review under its separate outbound block. Independent read-only checks establish:

- One successful encrypted archive, 519,920 bytes, with exact recorded SHA-256 and the age-format header.
- TEST schema 40, captured watermark 73, two customers, five document versions, zero financial movements/journal lines.
- Six manifest files: one consistent database snapshot and all five managed original/corrected PDFs. Each source PDF's path, size, and hash matches after normalizing Windows path separators.
- Original document snapshots, source coverage, milestones, decisions, private-field exclusions, and five PDF hashes remain exact. Integrity and foreign keys pass.

This verifies local encrypted backup and staged validation on the development computer while app network access is blocked. It does not substitute for physical offline Surface use, activation of a recovered copy in this fixture, removable-media loss testing, or restoration on a second Windows computer. Those release checks remain open. Google setup is optional and is not an offline-release gate.
