# Finish the offline release

Updated November 13, 2026. The app has a [verified Windows preview](windows-preview.md), but release work remains. The preview uses immutable feature source `96da46d780ccac01f369a5786e07186251ea9844`; its full functionality is still being integrated into `main` through focused pull requests. A working preview does not establish completion of every release scenario.

## Implemented and checked on the development computer

The preview includes owner sign-in and approvals, customer and booking records, client documents/PDFs, payments and bookkeeping, resources, bank reconciliation, reports, TEST/LIVE separation, and local encrypted backup/recovery. The interface uses the selected emerald, cream, and subtle-gold palette.

The [dated development record](https://github.com/edcadet10/sarah-beauty-desktop/blob/96da46d780ccac01f369a5786e07186251ea9844/docs/build-progress.md) distinguishes code, component tests, and actual native walkthroughs. Recent native evidence covers retained multiline input, split-payer allocations, period close/reopen, supplier and resource workflows, and balanced reports. The [offline backup check](https://github.com/edcadet10/sarah-beauty-desktop/blob/96da46d780ccac01f369a5786e07186251ea9844/docs/offline-backup-review.md) verifies an encrypted local archive while the app's outbound network access is blocked. The [installer rehearsal](https://github.com/edcadet10/sarah-beauty-desktop/blob/96da46d780ccac01f369a5786e07186251ea9844/docs/current-offline-installer.md) covers interrupted install/update retries and uninstall preservation. The [full-volume rehearsal](https://github.com/edcadet10/sarah-beauty-desktop/blob/96da46d780ccac01f369a5786e07186251ea9844/docs/native-full-volume.md) covers actual out-of-space failures and successful retries for posting, customer save, and receipt import.

These checks use fictional records on the development computer. They do not establish the pending device and release results below.

## Complete the remaining work

1. Finish the small-PR integration queue. Review each diff, resolve actual failures, and preserve successful checks for its application/test inputs. Keep the preview source distinct from `main` until integration catches up.
2. Review the remaining entry forms for supporting-file selection, draft replacement/navigation, keyboard operation, larger text, and theme behavior. Complete missing cases and fix observed problems in focused changes. Existing form and navigation checks do not establish universal coverage.
3. Run the [offline device checklist](https://github.com/edcadet10/sarah-beauty-desktop/blob/96da46d780ccac01f369a5786e07186251ea9844/docs/offline-device-checklist.md) on the actual Surface Pro 7: physical touch and keyboard, display scaling, disconnected customer/booking/PDF/approval/report workflows, close/restart, and retained records.
4. Restore and activate a complete local archive on a second Windows computer while disconnected. Check permanent IDs, supporting files, reports, owner access, and the retired prior writable instance. Same-computer archive validation and activation do not satisfy this requirement.
5. Finish clean-machine installation/update review, relevant interruption/power checks, dependency-license review, and the distribution/signing decision. The current download is an unsigned preview. No paid service or signing purchase is authorized by this checklist.
6. Before actual LIVE use, enter and review the real business, owner, tax, account, opening-balance, and accepted-terms facts through the private workflows that require them. Keep unknown values unknown. Fictional development setup does not confirm those facts.

## Keep online services optional

No Google account, Cloud project, OAuth client, or internet connection is required for local business work, PDFs, encrypted backup, or recovery. Real Google authorization/delivery acceptance applies only if the owners later enable those optional outputs. No Google project or client was created during the stopped setup attempt.

Keep local archives until an explicit retention policy is chosen; do not invent automatic deletion rules to close a checklist item. Preserve the [acceptance criteria](acceptance-criteria.md) and record each actual device result before marking it passed.
