# Schedule local encrypted archives

## Enable a schedule

1. Save and confirm [portable recovery material](desktop-local-backups.md).
2. Open **Backups and recovery** and expand **Automatic local archives**.
3. Select **Choose automatic archive folder**. Choose an existing private folder with the Windows folder picker.
4. Select **Estimate archive space**. Review the source size, archive allowance, temporary working allowance, and available space. Unknown capacity stays unknown. See [Estimate local archive space](archive-space-estimate.md).
5. Enter an interval from 1 to 10080 minutes, select **Enable automatic local archives while the app is open**, and select **Save automatic archive schedule**.

The default is unconfigured. Saving enabled consent permits this instance to create new archives using the already confirmed public recipient. The app checks pending work every 15 seconds while open, including at the owner sign-in screen. It does not run while closed or promise execution while Windows sleeps. Captures do not unlock or renew human access.

## Review pending work

**Refresh automatic archive status** shows the last observed pending and archived watermarks, next eligible attempt, consecutive failures, and fixed failure code. The runtime label updates without refreshing or extending the owner session. **Refresh backup health** loads individual attempts; their details distinguish automatic schedule/revision, manual requests, and historical unconfirmed instances.

After a completed archive, later changes wait for the saved interval. Unchanged covered records do not create another file. Failure/cancellation keeps pending work and uses [bounded retry](automatic-archive-queue.md). Existing complete files remain intact. Capacity estimates reserve no space; review failed attempts if the destination becomes unavailable or fills.

Navigation preserves unsaved schedule fields. **Reload saved archive schedule** explicitly replaces those fields. Navigating away does not stop enabled automatic work. To disable it, clear the enable checkbox and save. Earlier archives remain. Current permission is checked again before publication.

Normal window close requests cancellation and waits for its current and previously stopping automatic loops. Restart resumes saved permission and pending state. Forced termination and physical power loss remain separate acceptance scenarios. No automatic pruning, Google upload, or Sheets export occurs.

## Verified on the development computer

The native fictional rehearsal selected a folder, displayed capacity, saved a one-minute schedule, created one archive while signed out, closed/restarted, retained the schedule/queue/history, independently verified ciphertext, and disabled scheduling without another publication. Read-only comparisons retained exact financial/document/evidence/link source rows and all three managed hashes, two movements/five balanced journal lines, an archive at watermark 33, and only two schedule audit increments. Staging was empty and the earlier fixture remained unchanged at watermark 32. All 670 .NET cases pass and Windows Debug has no warnings/errors. This is not Surface or second-device acceptance.

The folder picker follows Microsoft's [Windows App SDK FolderPicker reference](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.storage.pickers.folderpicker).
