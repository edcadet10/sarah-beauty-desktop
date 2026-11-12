# Change your sign-in passphrase in the desktop app

Open **Owner security**. Enter your current phrase and repeat a distinct new word-based phrase of 15–256 characters. Review the account change before confirming. After saving, sign in again with the new phrase. Earlier sessions for that owner lose access through the current-credential check; the other owner's access stays valid.

Secret fields clear after each attempt, cancellation, and navigation away. Leaving this page clears/cancels directly through workspace navigation as well as unload cleanup. A successful completion callback checks the original owner-session reference before changing the current sign-in screen. Operations are registered for shutdown and support cancellation. No phrase is displayed in the confirmation or saved in audit.

The first native walkthrough exposed missing cancellation feedback after fast navigation because it depended on page-loading events. The revised flow handles page departure directly and does not suppress status on that event timing. The complete fictional native walkthrough now passes private controls, navigation/cancel clearing, repeat mismatch, wrong current phrase, reviewed change, immediate sign-in, old-phrase rejection, unchanged other-owner sign-in, new-phrase restart, and graceful close. Windows Debug has zero warnings/errors.

Read-only checks preserve schema 35, two owners, watermark 2, exactly one passphrase-change audit, no remaining failed attempts after fresh sign-in, no financial/document/evidence/customer/ownership/Google records, no plaintext fixture phrases, and valid SQLite integrity/foreign keys. The complete local Release suite, including cryptographic archive/recovery cases with the built helper, passes all 1,288 tests in 6 minutes 3 seconds. No real user credential or Google authorization was changed. Hosted/device/full-release acceptance remain separate checks.

Forgotten-phrase recovery uses the existing reviewed encrypted-archive flow from sign-in: portable recovery material, a reviewed owner selection and new phrase, and a newly prepared writable instance. This page does not bypass those requirements or silently reset another owner's phrase.
