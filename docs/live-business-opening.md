# Open an existing LIVE business without creating a replacement

The public LIVE opening service resolves only the published `Primary/sarahbeauty.db`. Checking whether the destination exists and opening a missing database create no folders or replacement data. A file or empty folder occupying `Primary` remains available for recovery review and blocks creation.

Opening requires the LIVE mode, active writable-instance identity, SQLite integrity and foreign keys, one business, two active owners and verified retained profile history. TEST data, empty databases, missing or altered profiles, broken references and prepared/retired copies are rejected. Ancestor, primary and database paths reject links/reparse points.

Incomplete current setup reviews remain editable after sign-in. Opening does not require operational readiness; the posting guard checks that separately. This lets an owner correct unconfirmed settings without bypassing the approval checks.

Thirty-six opening/creation/mode-store cases pass. Rejected current-schema fixtures preserve their bytes, including a TEST database placed in the LIVE location. The first corruption run stopped two fixtures at immutable-profile triggers; the corrected fixtures explicitly remove only those guards in temporary data and reach the intended opening rejection. Windows Debug and native TEST profile reopen/lock/close pass; independent checks preserve the existing profile fixture and managed file.

The full local regression at creation/opening source `fd4adc7` passes all 1,477 cases in 7 minutes 21 seconds. [Hosted verification](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43253413581) passes the same 1,477 cases in 14 minutes 23 seconds, Windows Debug in 55 seconds with zero warnings/errors, and recovery cryptography. The earlier guard regression passed 1,449 cases at `0fadb0a`. Native mode selection and reviewed creation, mode-aware recovery activation, external delivery and release acceptance remain pending.
