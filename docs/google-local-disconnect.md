# Disconnect Google from this local instance

Status: The owner-authorized local disconnect service and [desktop controls](desktop-google-connection.md) are implemented. Tests use fictional grants only; no Google request occurs during local disconnect.

Disconnect requires an active owner and the reviewed connection identity. It records a fresh disconnected generation, prior connection identity, actor/time, and one audit. It preserves the last verified account/storage facts and client configuration. Repeating the same disconnected decision creates no extra audit.

Background grant loading stops when the local disconnection commits. Earlier in-flight consent cannot overwrite that decision because its reviewed generation is stale. A later explicit reconnect must use the newly reviewed disconnected generation and fresh consent.

Token cleanup happens after the decision and rechecks the disconnected generation inside the file/database reservation. Delayed cleanup cannot delete a newer connection's grant. If the token file is unavailable, the result distinguishes incomplete cleanup from the already committed local disconnection. Retrying can clear the file once available. A retained protected file does not restore local authority.

This operation does not revoke Google's server-side grant or establish that other devices disconnected. Use the desktop link to [Google account access settings](https://myaccount.google.com/connections) to manage the server grant, following [Google's connection guidance](https://support.google.com/accounts/answer/13533235). Local disconnection, token cleanup, server access, and Drive/Sheets delivery remain separate facts.

Five new disconnect cases pass with ten grant and seven credential-file cases (22 targeted cases): audit/idempotence/reopening/reconnect, foreign/locked/stale access, unavailable cleanup/retry, in-flight consent invalidation, and replacement-grant preservation during delayed cleanup.
