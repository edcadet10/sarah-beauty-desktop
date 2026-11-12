# Require fresh consent after a rejected Google grant

Status: Persistent reconnect state is implemented. Desktop connection controls, real Google acceptance, and cloud delivery follow separately. Verification uses fictional failures and grants.

Recognized grant/client rejection, unexpected scope, incomplete offline material, or another returned Google account records a fixed issue bound to the exact business, instance, client, and connection generation. The record contains no provider message or credentials. It does not advance the business watermark or renew a human session.

Loading/access stops for the rejected generation and reports **Reconnect required** after restart. It does not repeatedly attempt renewal. Network, timeout, quota, and other temporary failures preserve consent for a later retry. Unknown provider codes are not saved as connection issues.

A delayed failure from an older connection or configuration cannot affect a replacement. Explicit successful consent clears the previous issue in the same connection-record transaction. Local disconnect remains available. If the issue cannot be recorded, the operation reports that local state is unconfirmed instead of claiming persistence.

Six new cases pass with eleven renewal and five disconnect cases (22 targeted cases): persistent rejection/no retry loop, unchanged locked access/watermark, restart/fresh consent, offline retry, old-failure isolation, unknown-detail exclusion, disconnect after rejection, and changed-client isolation. The previous account-mismatch test was updated to inspect retained protected bytes directly because rejected grants can no longer be loaded for use.
