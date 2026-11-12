# Deliver encrypted archives to Google Drive

Status: Private-folder setup, saved delivery permission, manual delivery, durable history/health, and app-lifetime polling are implemented. Real Google consent/account delivery, actual device acceptance, and second-device recovery remain pending. The native rehearsal used an unconnected fictional fixture and made no Google request.

## Enable the destination

1. [Connect the intended Google account](desktop-google-connection.md).
2. In **Backups and recovery**, expand **Google Drive archives**.
3. Select **Create private Google archive folder**. Review the saved folder and verification facts. Creating another folder resets delivery to disabled and retains earlier Google files.
4. Select **Enable delivery of completed encrypted local archives to Google Drive while the app is open**, then select **Save Google archive delivery choice**.

The app verifies sole private ownership/access before saving enabled permission. It requests no sharing invitation or storage purchase. To withdraw Drive delivery, clear the checkbox and save. A pure permission reduction can save locally while offline. An already accepted remote file can remain; disconnection is a separate choice.

## Keep new changes eligible

[Configure automatic local archives](desktop-automatic-backups.md), or select **Create encrypted archive now**. Drive delivery uses completed archives from the current writable instance and recovery recipient. The queue reports **Waiting for local archive** when newer business changes have no complete eligible local archive.

While the app is open, a separate loop checks delivery every fifteen seconds, including before local sign-in. It respects saved permission and persisted backoff. Local archive creation runs independently, so slow Google work does not occupy the local scheduling loop. Closing the app, changing the selected instance, or retiring it cancels its earlier loops. Normal close awaits cleanup. No work runs while the app is closed or asleep.

To request delivery explicitly, select a completed local archive and select **Deliver selected local archive now**. Saved Drive permission still applies. The app pins and hashes ciphertext, reuses reserved file identities, verifies remote completion/private access, and retains individual attempt outcomes. Later business changes do not expand an earlier archive's watermark.

## Review outcomes

Select **Refresh Google archive health and history** to review coverage, last verified success, pending changes, retry state, and history. The live worker line updates without touching a human session. Select a delivery and **Show Google archive delivery attempts** for its source instance, exact coverage, file/hash facts, and attempts. Refresh does not discard the unsaved delivery checkbox. **Reload saved Google archive choice** restores the saved fields.

Select **Cancel Google archive operation** to cancel a foreground operation. Leaving the view also cancels it. Foreground work registers with coordinated app close. Cancellation or failure after remote acceptance can leave a file; review saved history before retrying. A later verification can confirm the same reserved file without uploading another copy.

Connection, Drive archive, Sheets export, and restore-test states remain separate. An uploaded archive does not establish a successful recovery on another device.

## Verification

All 837 .NET cases pass; Windows Debug has no warnings/errors. The native fictional rehearsal verified default-off/unconfigured controls, guards for missing account/folder/source/history selection, unsaved checkbox preservation through navigation, explicit reload, signed-out close, and restart with both loops. Read-only checks retained the same financial/document/evidence/link rows, three managed hashes, balanced journal facts, one earlier configuration-only audit, and unchanged original fixture. No Google token, connection, destination, or delivery was created. Actual Google consent/upload and Surface/second-device acceptance remain separate checks.
