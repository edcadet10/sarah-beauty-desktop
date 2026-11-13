# Verify the current offline preview installer

Date: November 12, 2026. Installer integration: `e2ea395`, using the previously reviewed guarded installer. Application source: `0a6185b`, TEST schema 40. Application source is unchanged by this integration.

The installer tooling is brought into the current offline feature branch. Only the progress document needs a merge resolution; both dated evidence sections are retained. Package-generation and native destination probes pass, including literal paths, rejected database/archive/key content, macro/link rejection, nonexistent destinations, alternate destinations, and junction ancestors.

## Actual interrupted installation and update

Two downloaded GitHub bundles supply exact manifests: older input-preservation source `8dda1a7` and current offline-backup source `0a6185b`. Both contain 574 files and retain preview file version 1.0.0.0. NSIS 3.13 builds separate private QA installers for an isolated destination with spaces.

1. Create unrelated top-level, nested, and SQLite-header sentinels in the designated test folder. Capture every file hash in the separate fictional business directory.
2. Start the older silent installer. Observe partial extraction, then terminate that exact owned process after 27 of 574 package paths exist. Both business and sentinel bytes remain unchanged.
3. Rerun the same installer. Every one of the 574 installed files matches its original manifest; the notice, shortcut, per-user location, and correctly quoted uninstall command are present.
4. Start the current updater. Observe a changed payload file matching the new manifest while the updater is still running, then terminate that exact owned process. Business and sentinel bytes remain unchanged.
5. Rerun the current installer. All 574 installed paths, sizes, and SHA-256 hashes match the current GitHub bundle, with valid per-user registration and retained sentinels.

These are actual process interruptions and successful retries. They establish recoverability by rerunning setup; they do not claim atomic rollback or successful operation from a partially copied installation.

## Installed app and removal

The installed executable lets the second owner read all edited loan terms and unchanged multiline evidence. Independent customer/loan checks retain exact text and permission history.

A temporary outbound Windows Firewall rule blocks only that installed executable. Native 1280/500-pixel checks retain the primary local backup action and collapsed optional online group. The second owner verifies the existing encrypted archive, gets rejection with an incorrect recovery passphrase, then decrypts and validates it with the correct fictional phrase. Schema 40, two customers, five documents, six manifest files, and their captured facts pass. The working records and archive remain unchanged. The owned firewall rule is removed afterward.

After capturing business-file hashes again, silent uninstall removes all packaged files, installer notice, uninstaller, Start menu shortcut, and per-user registration. Every separate business-file byte and all unrelated sentinels remain exact. The private QA app is no longer installed.

The current QA installer is 115,402,535 bytes, SHA-256 `E1138AA19304C931555080F980AA8C05504CDF10EA0DA9FC13C0F08A27D64963`. Private fixtures and QA installers remain outside Git. No QA installer is distributed or publicly released.

Clean-machine installation, actual Surface touch/scaling/offline operation, physical power/disk failure, signing/distribution review, and second-computer recovery remain separate. Google setup is optional and does not gate the offline release.
