# Unlock with a password or a local PIN

The owner requested both password and PIN unlock on November 13, 2026. The desktop now offers **Password / passphrase** and **PIN**, with a separate PIN setting for each owner. Password sign-in remains available.

## Set your PIN and unlock

1. Sign in with your owner password. Open **Owner security**.
2. Enter your current password, a new PIN with 6 to 12 digits, and the same PIN again. Select **Save my PIN**.
3. Select **Lock / switch owner**, review the unfinished-work message, and select **Lock now**.
4. Select your owner, choose **PIN** under **Unlock with**, enter your PIN, and select **Sign in**.

To use your password, choose **Password / passphrase** instead. To remove a PIN, open Owner security, enter your current password and select **Remove my PIN**. PIN and password fields clear after an attempt, when you switch methods/owners, or when you leave their view. Captured sign-in controls remain disabled while authentication runs.

## Keep a separate PIN for each owner

Setting or removing your PIN requires your active owner session and current password. A PIN contains 6 to 12 ASCII digits, including any leading zeros, and must be repeated exactly. It does not replace the owner's password or password confirmation for approvals.

PIN material is salted and derived through the existing 600,000-iteration PBKDF2-SHA256 primitive, with a PIN-specific purpose string. Windows CurrentUser DPAPI protects that verifier in private credential storage outside the business folder. Its owner identity, password-credential stamp, business, TEST/LIVE mode and writable-instance context must match. Local PIN files are not part of portable business archives. After recovery into a new instance or a password change, sign in with your password and configure a PIN again.

PIN and password failures share the existing owner counter: five combined failures temporarily block either method. Current lockout and password credentials are rechecked before returning a session. The protected-file lease remains held through the PIN authentication decision, so replacement/removal serializes with a reviewed PIN. Locked, changed or foreign owner access cannot set or remove a PIN. Missing/unreadable local PIN material leaves password sign-in available.

## Try to unlock the wrong owner

The claim is that this service authenticates only the selected current owner and retains the password fallback. A cross-owner unlock, bypass of shared lockout, successful stale-password review, exposed archive verifier, or a PIN accepted as password confirmation rejects the claim.

The 46 selected PIN/shared-credential cases and a separate new-instance case pass. They cover distinct owners, leading-zero PINs, invalid/mismatched input, current-password authorization, removal, combined lockout, password-change invalidation, changed credentials during review, copied/damaged protected files, cancellation, locked sessions, archive exclusion, and password confirmation after PIN sign-in. A new writable-instance identity rejects the old PIN and accepts a newly configured one after password sign-in. The shared-file probe blocks replacement until a review finishes and clears its temporary plain-byte buffer. Existing Google credential tests remain green; no Google connection is required or created.

## Native desktop checks

A separate fictional TEST business retains one customer and one $2 expense draft. Actual UI checks validate PIN format/current-password requirements, configure distinct PINs for both owners, unlock with either method, reject the other owner's PIN, and preserve every saved table through configuration/unlock. A PIN-unlocked owner must still enter the password to approve the expense; it posts once, with two journal lines balanced at 200 cents per side and the authenticated Alex approver retained. The other PIN-unlocked owner reads the same posted source.

Five actual wrong PIN attempts block the correct password too. The real timeout expires before password sign-in succeeds; every saved table then matches the prior checkpoint. Changing the password invalidates the old PIN, password fallback permits setting a replacement, and removing the replacement leaves password access working. The other owner's PIN remains configured. Independent rows retain the customer, posted expense, balanced journal, correct approver, integrity and foreign keys.

Packaged restart retains the other owner's PIN, changed-password fallback and every saved business table. At 1280/500 window widths, the sign-in and settings controls fit; both unlock methods work at 500, sensitive inputs expose the protected-input flag, both settings actions are reachable, and the enabled/unavailable statuses match each owner. App-only captures were visually inspected. The initial narrow-window harness looked for a collapsed Customers menu item; the corrected check uses the actual signed-in owner header. These checks do not establish physical touch, keyboard or display-scaling acceptance.

A held credential file also reproduces an out-of-order status bug: a rejected edit could leave Checking status stale. A versioned asynchronous refresh after an attempt fixes that case with every stored table retained. Save my PIN uses the existing emerald primary style; Remove remains secondary. No new palette colors are introduced.

Microsoft describes [CurrentUser DPAPI protection](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata) as protection using Windows credentials. Network-blocked use, full regression and GitHub download acceptance remain separate checks.
