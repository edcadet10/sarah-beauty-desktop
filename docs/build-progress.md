# Build progress

This file tracks the full application build requested on November 10, 2026. A component marked implemented still needs the release checks applicable to it. The app is not ready for live business use.

| Area | Current state | Evidence or remaining work |
|---|---|---|
| Local storage | Implemented | Verified pre-upgrade snapshots, atomic migrations, explicit TEST mode |
| Owner setup and sign-in | Implemented | Two owners, salted PBKDF2-SHA256 hashes, short lockout after failed attempts, expiring store-bound sessions, transaction audit |
| Customers | Pending | Form, search, editing, permission history, archive |
| Bookings | Pending | Charges, places, appointments, exact payment plans |
| Client documents | Pending | Versioning, source coverage, PDF output |
| Books and approvals | Pending | Atomic balanced posting, payments, credits, refunds, closed periods |
| Resources | Pending | Supplies, suppliers, assets, loans, owner movements, mileage |
| Reconciliation and reports | Pending | Statements, bounded matches, reports, dashboard |
| Recovery and Google | Pending | Attachments, portable encrypted archive, Sheets export, Drive authorization/readback |
| Release | Pending | Installer, full fictional walkthrough, actual Surface and second-device checks |

## Owner access checks

The 34 automated storage and access cases pass using fictional temporary stores. Checks include sign-in after reopening, distinct salts, persisted failed-attempt lockout, invalid/repeated setup, cross-store and expired-session rejection, and rollback of business writes. No actual owner names or passphrases are committed.

Storage now lives in `src/SarahBeauty.Business`; tests reference the same compiled project that WinUI uses. Schema 2 introduces Business, Owners, and Audit. Existing schemas 0 and 1 are snapshotted before the upgrade. The interface offers setup, sign-in, and lock/switch owner; interface automation and device checks remain pending.

Password hashing follows [OWASP's PBKDF2 guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) and uses [.NET's PBKDF2 implementation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2). Local sign-in controls application actions; database files also require appropriate Windows account and device protection before live use.
