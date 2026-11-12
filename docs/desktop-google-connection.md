# Connect the app to Google

Status: Desktop client import, browser consent, saved connection review, cancellation, and local disconnect are implemented. Drive archive delivery and readable Sheets export follow separately. Connecting does not upload business records.

## Prepare this app's Desktop client

1. Create or select the app's Google Cloud project without billing enrollment. Enable the Drive and Sheets APIs.
2. Configure the project's Google Auth Platform audience and requested access. Use the intended account as a test user while the project is in Testing. Google can limit testing grants; review the current [OAuth consent configuration](https://developers.google.com/workspace/guides/configure-oauth-consent) before everyday use.
3. Create an OAuth client of type **Desktop app**, following [Google's installed-app guide](https://developers.google.com/identity/protocols/oauth2/native-app). Download its JSON file privately. A Web client or another connected application's tokens cannot replace this app's Desktop configuration.
4. Sign in to Sarah Beauty, open **Backups and recovery**, and expand **Google connection**.
5. Select **Import Google Desktop client** and choose the downloaded JSON. Review the project and configuration revision. Identical imports retain the revision.

The app protects the configuration outside the business directory with Windows-profile protection. It does not show its secret text or include device credentials in portable archives. A restored instance or another Windows profile needs its own connection setup. Protect the downloaded source file separately; import does not delete it.

## Authorize the intended account

1. Select **Connect Google account**.
2. In the system browser, select the intended Google account and review consent. Enter Google passwords and MFA only in Google's browser flow.
3. Return to the app. Review the verified account, observation time, and any observed free storage. Missing capacity stays **Unknown**. An earlier observation does not reserve storage.

The app requests `drive.file` access, uses S256 PKCE and a state-checked loopback callback, and verifies account facts before saving protected tokens. The [scope permits app-created or user-selected files](https://developers.google.com/workspace/drive/api/guides/api-specific-auth). It does not authorize arbitrary Drive browsing. A saved connection does not establish that a later request will succeed; Google can reject or expire access.

Select **Cancel Google connection operation** to cancel a pending operation. Cancellation can arrive after a saved decision; refresh its saved state before retrying. Leaving the view cancels its pending work. Normal app close cancels registered connection work and waits for cleanup.

## Disconnect or remove Google's access

Select **Disconnect Google on this device** to stop this instance's saved authority and attempt protected token cleanup. The result reports incomplete cleanup separately. Existing business records, Google files, and client configuration remain available.

To remove the server grant, select **Open Google account access settings** and follow [Google's instructions for managing third-party connections](https://support.google.com/accounts/answer/13533235). Local disconnect does not establish server revocation or another device's state. Review the app's saved status before a later explicit reconnect.

## Verification and limits

The Windows Debug build passes with no warnings or errors. A fictional native rehearsal imported a Desktop configuration, restarted, imported it again without another revision/audit, and retained **Not connected**. Read-only checks confirmed protected configuration outside the business folder, no Google token or connection, unchanged financial/document/evidence/link rows and three managed hashes, and one configuration-only watermark increment. The earlier fixture stayed unchanged.

No real Google browser consent, password/MFA entry, account upload, Surface acceptance, or second-device authorization was performed. Drive delivery, Sheets export, and restore-test success remain separate states.
