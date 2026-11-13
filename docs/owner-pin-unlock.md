# Unlock with a password or a local PIN

The owner requested both password and PIN unlock on November 13, 2026. Password/passphrase sign-in remains available. The first focused change adds the local PIN service; the desktop selector and settings are the next change and are not claimed implemented by this service alone.

## Keep a separate PIN for each owner

Setting or removing your PIN requires your active owner session and current password. A PIN contains 6 to 12 ASCII digits, including any leading zeros, and must be repeated exactly. It does not replace the owner's password or password confirmation for approvals.

PIN material is salted and derived through the existing 600,000-iteration PBKDF2-SHA256 primitive, with a PIN-specific purpose string. Windows CurrentUser DPAPI protects that verifier in private credential storage outside the business folder. Its owner identity, password-credential stamp, business, TEST/LIVE mode and writable-instance context must match. Local PIN files are not part of portable business archives. After recovery into a new instance or a password change, sign in with your password and configure a PIN again.

PIN and password failures share the existing owner counter: five combined failures temporarily block either method. Current lockout and password credentials are rechecked before returning a session. The protected-file lease remains held through the PIN authentication decision, so replacement/removal serializes with a reviewed PIN. Locked, changed or foreign owner access cannot set or remove a PIN. Missing/unreadable local PIN material leaves password sign-in available.

## Try to unlock the wrong owner

The claim is that this service authenticates only the selected current owner and retains the password fallback. A cross-owner unlock, bypass of shared lockout, successful stale-password review, exposed archive verifier, or a PIN accepted as password confirmation rejects the claim.

The 46 selected PIN/shared-credential cases and a separate new-instance case pass. They cover distinct owners, leading-zero PINs, invalid/mismatched input, current-password authorization, removal, combined lockout, password-change invalidation, changed credentials during review, copied/damaged protected files, cancellation, locked sessions, archive exclusion, and password confirmation after PIN sign-in. A new writable-instance identity rejects the old PIN and accepts a newly configured one after password sign-in. The shared-file probe blocks replacement until a review finishes and clears its temporary plain-byte buffer. Existing Google credential tests remain green; no Google connection is required or created.

Microsoft describes [CurrentUser DPAPI protection](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata) as protection using Windows credentials. Actual desktop PIN setup/unlock, network-blocked use, full regression and downloaded-app acceptance remain separate checks.
