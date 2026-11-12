# Save a verified Google connection

Status: The owner-authorized verified-grant service is implemented. Local disconnect, refresh, desktop controls, real consent/account acceptance, and cloud delivery follow separately. Tests use fictional consent/account responses only.

Connection requires an active owner and the reviewed current client/connection identities. Consent and account verification run outside database writer reservations. The service checks complete narrow offline material, snapshots its token fields, verifies the authenticated account, and rechecks owner access, cancellation, and unchanged generations before publishing the protected grant with its connection record and audit.

The public record contains business/instance/client generation, account/storage observation, owner, and verification time. Access/refresh tokens remain in Windows-profile protected files outside the business directory. Grant save advances the business watermark once; reading credentials does not advance it or renew a human session.

Background loading requires this instance's matching client/connection/file generations and an active consenting owner. Client changes, deactivated consent, stale instances, damaged files, or unconfirmed publication prevent reuse. File and database publication do not form one cross-resource transaction; generation checks reject mixed state after an interrupted commit, and explicit fresh consent can repair it.

Connection health checks local protected-file binding. It does not establish current remote authorization, reserve Google storage, or mark Drive/Sheets delivery complete. Expired-token refresh and server revocation checks remain separate work. A locked human session stays locked while an already authorized internal grant load can complete. Restored instances reconnect separately.

Ten new grant cases and eight client-settings cases pass (18 targeted cases): verified protected reopen, archive-secret exclusion, unchanged locked access, foreign/locked/unconfigured requests, owner lock/configuration changes during consent, account failure/cancellation, unconfirmed publication/repair, concurrent completion, unavailable-file recovery, and restored/deactivated consent. Windows Debug has no warnings/errors. Latest complete regression result: 726 passing cases before the ten grant additions.

See [Google consent protocol](google-oauth-protocol.md), [account verification](google-account-verification.md), and [protected device files](google-device-credentials.md).
