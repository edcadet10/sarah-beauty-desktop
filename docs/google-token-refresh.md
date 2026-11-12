# Renew Google access without unlocking an owner

Status: Cached/renewed access, protected refresh publication, and [persistent reconnect state](google-reconnect-state.md) are implemented. Desktop controls, real Google acceptance, and cloud delivery follow separately. Tests use fictional grants/responses.

An internal access request loads the current protected grant and saved consent. A token with more than two minutes remaining is reused without a network request. Expiring tokens renew under an exclusive file lease outside database writer reservations. Other readers reload after obtaining the lease, so one completed renewal serves concurrent callers.

The pinned Google library posts refresh material to the official token endpoint, with no browser or disk token store. Its default transport disables redirects and cookies. Omitted refresh material retains the earlier value, and returned scope/material is validated. Renewal verifies the same authenticated Google permission identity before protecting new bytes. Final publication rechecks cancellation and unchanged connection/client/owner consent.

Token maintenance creates no business audit or watermark increment and does not touch human sessions. It preserves the original verified connection record; its account/storage observation remains from that recorded verification. Each network renewal has a sixty-second deadline, and lease/renewal work has a two-minute operation limit. Explicit caller cancellation remains distinct. Failures release the lease and preserve earlier token bytes when publication has not completed.

Eleven new refresh cases pass with eleven consent and five disconnect cases (27 targeted cases). They cover cache reuse, locked-session preservation, omitted refresh material, protected reopening/no audit loop, three concurrent readers, changed configuration/disconnect during renewal, another account, cancellation/lease release, actual SDK POST construction, and private-safe invalid/broad responses. No Google browser, account, connected-app token, or actual remote renewal was used.
