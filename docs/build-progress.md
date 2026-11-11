# Build progress

This file tracks the full application build requested on November 10, 2026. A component marked implemented still needs the release checks applicable to it. The app is not ready for live business use.

| Area | Current state | Evidence or remaining work |
|---|---|---|
| Local storage | Implemented | Verified pre-upgrade snapshots, atomic migrations, explicit TEST mode |
| Owner setup and sign-in | Implemented | Two owners, salted PBKDF2-SHA256 hashes, short lockout after failed attempts, expiring store-bound sessions, transaction audit |
| Customers | Implemented | Name-only save, contact warning, validated optional contact fields, search/edit/archive, permission history, stale-edit guard |
| Bookings | In progress | Persistent charges, places, recipient links, exact plans, and appointments implemented; confirmation awaits approved-payment workflow |
| Client documents | In progress | Quote versions/acceptance and bounded single/split-payer invoice conversion implemented; PDFs, receipts, and credits pending |
| Books and approvals | In progress | Atomic balanced posting, authenticated approvals, core receipts/expenses/transfers, financing, bounded refunds/reversals, historical reports; applications/credits and period-close UI pending |
| Resources | Pending | Supplies, suppliers, assets, loans, owner movements, mileage |
| Reconciliation and reports | Pending | Statements, bounded matches, reports, dashboard |
| Recovery and Google | Pending | Attachments, portable encrypted archive, Sheets export, Drive authorization/readback |
| Release | Pending | Installer, full fictional walkthrough, actual Surface and second-device checks |

## Owner access checks

The 34 automated storage and access cases pass using fictional temporary stores. Checks include sign-in after reopening, distinct salts, persisted failed-attempt lockout, invalid/repeated setup, cross-store and expired-session rejection, and rollback of business writes. No actual owner names or passphrases are committed.

Storage now lives in `src/SarahBeauty.Business`; tests reference the same compiled project that WinUI uses. Schema 2 introduces Business, Owners, and Audit. Existing schemas 0 and 1 are snapshotted before the upgrade. The interface offers setup, sign-in, and lock/switch owner; interface automation and device checks remain pending.

## Customer checks

Schema 3 adds customer records and permission history. The 40 automated cases include name-only save, contact warning removal/restoration, invalid input rejection, distinct IDs for duplicate names, archive filtering, permission withdrawal history, and stale-edit rejection. The WinUI customer form exposes the same operations. These checks establish service/database behavior; device and interface acceptance checks are still pending.

Password hashing follows [OWASP's PBKDF2 guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) and uses [.NET's PBKDF2 implementation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2). Local sign-in controls application actions; database files also require appropriate Windows account and device protection before live use.

## Pricing checks

The 48 automated cases include the three agreed wedding calculation fixtures, odd-cent deposits, explicit no-trial custom plans, invalid money/overflow rejection, and deterministic proportional cent allocation. Money uses integer cents; quantities use decimal arithmetic. Allocation uses integer arithmetic, including amounts at the supported 64-bit limit. These helpers will be used by booking and document commands.

## Booking checks

Schema 4 adds a shared revisioned store for typed operational aggregates. Booking commands validate referenced customers, named/unnamed counts, retained identities, valid charges, and artist conflicts in the same transaction as the save. Removed unnamed places remain in history. Appointment times carry explicit offsets; overlapping work for the same artist needs an override reason. Status changes retain their actor and reason and never create money movements.

The suite now has 53 passing cases. Added cases cover six unnamed guests with two named later, quantity reduction/increase, explicit tax resolution, no-trial plans, artist overlap/separate-artist handling, and rejection of direct confirmation or cross-business customer IDs. Windows Debug build passes. Booking confirmation is deliberately unavailable until approved deposits can be checked; document, financial, interface, and Surface checks remain pending.

## Financial posting checks

Schema 5 adds accounts, posting batches, immutable movements, journal lines, and book settings. The Money screen prepares drafts, previews the saved journal, and requires the signed-in owner's passphrase before approval. Drafts do not affect posted totals. Both owners can approve their own work or the other owner's work. Posted records have linked corrections rather than editable amounts.

The ledger tests cover the agreed $1,170 cash / $300 asset / $400 debt / $120 result fixture, gross processor receipts/fees/payouts, stale drafts, incorrect passphrases, reliable-reference duplicates, historical principal limits, reimbursement, bounded refunds, exact reversals, closed dates, concurrent approvals, historical reports, and depreciation limits. Injected failures after batch, movement, journal-line, and audit writes roll back the transaction; retries post once.

Invoice applications, customer credit funding, reconciliation dependencies, full asset disposal, period-close/reopen screens, and complete financial workflows are still being built. These core checks do not mark every financial acceptance scenario passed.

## Quote version checks

Schema 6 stores numbered documents, immutable version snapshots, acceptance evidence, and pending render artifacts. The quote screen prepares and reviews saved drafts, confirms the owner before issuance, and records acceptance of an exact version. Issuance retries retain the same number/version. Issuing a proposed revision leaves an earlier accepted agreement intact until the new version is accepted. Unknown tax, unresolved payment plans, changed source bookings, and invalid recipients block issuance.

Three additional cases verify frozen customer/event/pricing data, exclusion of private sentinel fields, same-number issuance retry, version-specific acceptance, and failure without consuming a document number. PDF rendering and invoice conversion are separate follow-up PRs. No document has been automatically sent.

## Invoice conversion checks

Schema 7 adds source coverage, explicit payer headers, invoice milestones, and a billing generation guard. Single-payer decisions cover all charges including travel; split-payer travel needs an explicit charge payer. Unpaid-share responsibility cannot remain undecided at issuance. Partial invoices consume accepted quantities and cents once; their milestone shares sum to the accepted booking plan. Retried/concurrent issuance cannot duplicate coverage.

Five new cases cover full and split-payer conversion, racing requests, tiny fractional-cent allocation, and rejection of an accepted amendment that would reprice billed charges. The fractional-cent probe initially stopped a valid later portion when repeated rounding exhausted the source cents; individual allocations are now capped at their remaining source cents, with an explicit rounding adjustment and final residual. Issued invoice data stays immutable. Applications, credit adjustments, and PDF output remain separate work.
