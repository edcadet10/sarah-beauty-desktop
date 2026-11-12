# Save the app's Google desktop configuration

Status: The owner-authorized configuration service is implemented. Desktop import/connection controls, saved grants, real consent, and delivery follow separately. Verification uses fictional configuration only.

An active owner can save validated downloaded Desktop client JSON. The service protects it in Windows-profile credential storage outside the business directory. Public metadata retains a generation identity, revision, instance/business, project, client-ID fingerprint, owner, and time. An audited change advances the business watermark once. Repeating identical client facts preserves the original generation and creates no extra audit.

Saving requires the reviewed current configuration identity and rechecks current owner access inside the final file/database reservation. Concurrent replacements permit only one matching generation. Changing the secret or client facts creates a new generation for later grant binding. The metadata contains no client secret or original downloaded JSON.

The file and SQLite commit cannot form one filesystem/database transaction. A mismatch after interrupted publication fails closed: loading refuses the new file with the older database generation. Current recorded metadata remains available for owner review. An explicit reviewed import can replace a damaged or mismatched file with validated configuration. Read failure alone preserves existing bytes. Input and serialized plaintext byte buffers owned by the service are cleared after use; parsed secret strings remain subject to managed-memory lifetime.

Eight new settings cases pass with seven credential-file cases (15 targeted cases): protected reopening, exact idempotence/audit counts, archive-secret exclusion, current-owner/revision checks, lock during flush, rolled-back writes, mismatched publication/reviewed repair, two concurrent replacements, and restored-instance isolation. Windows Debug has no warnings/errors. The current NuGet audit reports no vulnerable Business dependencies; this is not a complete release security or device acceptance result.

See [Validate the app's Google desktop client](google-desktop-client.md), [Protect Google device credentials](google-device-credentials.md), and [Authorize a Google desktop connection](google-oauth-protocol.md).
