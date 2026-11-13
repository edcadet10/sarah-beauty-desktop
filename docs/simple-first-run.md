# Start with three details

1. Open Sarah Beauty Desktop.
2. Enter **Business name**, **Username**, and **Password**. Use at least 15
   characters for the password; a few memorable words work well.
3. Select **Create my workspace**. The dashboard opens immediately.

There is no second-user form, business questionnaire, or required TEST/LIVE
choice before entering a fresh workspace. Existing workspaces open at sign-in
and retain their records.

## Complete information later

Open **Business settings > Business setup** when you want to add business
details. Unprovided legal, contact, tax, banking, date, and ownership facts remain
unknown. Creating your workspace does not confirm those facts.

To add another user later, open **Business settings > Owner security > Add
another user later**. Enter their username and password, then authorize the
addition with your current password. Ownership percentages remain separate.
Optional PIN setup is on the same security page. Financial actions retain their
existing review and password requirements.

## Verification on November 13, 2026

The previous setup required two users upfront and a detailed LIVE profile. The
new service and screen are separate changes in PRs #362 and #363.

- All 1,709 storage/business regressions pass locally in 7 minutes 51 seconds.
- Windows Debug and self-contained Release publishing succeed with no build
  warnings or errors; runtime notices are verified for 29 packages.
- A fresh, isolated LIVE Debug run shows exactly three setup fields, handles
  missing/short input, and reaches a loaded dashboard without further setup.
  Business details and a second user can be added afterward. Lock/unlock and
  normal close/restart/sign-in pass.
- The published Release executable passes the same setup and restart path in
  an isolated TEST folder. PIN configured afterward, PIN unlock, password
  fallback, and PIN removal also pass.

The native harness initially stopped at the existing lock confirmation. Its
completed run explicitly confirms **Lock now** and **Close app**. The app's
confirmation and authentication checks were preserved.

These checks use fictional records on the development computer. Physical
Surface, second-device, signing, and final release acceptance remain separate.
