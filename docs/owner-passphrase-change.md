# Change your own sign-in passphrase

The change service requires an active session bound to the current credential and verifies the current phrase through the existing sign-in/lockout path. The new word-based phrase must be 15–256 characters, differ from the current phrase, and match its repeated entry exactly. New hashing runs outside the writer. The writer then rechecks the original session/credential before saving the new salted hash, resetting failed-attempt state, and adding one audit/watermark change.

Successful completion locks the initiating session. Other sessions for the same owner fail their next protected/current-access check because the credential binding changed. The other owner's credential and session remain valid. The next use requires fresh sign-in with the new phrase. No phrase is stored in audit or returned by the service.

All 18 related Release cases pass, including ten new change cases for reopen/new sign-in, same-owner session revocation, other-owner preservation, current-phrase lockout, invalid/mismatched/unchanged new phrases, post-write cancellation/lock/storage rollback, a competing change after initial verification, cancelled start, and foreign access. Rollback preserves both the original credential and audit/watermark. Business Debug has zero warnings/errors. Desktop controls and native/recovery regression follow separately.

This implements active-session and current-password verification from the [OWASP authentication guidance](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#change-password-feature). It uses the app's local credential model and does not request Google authorization.
