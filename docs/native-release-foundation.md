# Check native setup, customers and owner approvals

On November 12, 2026, a fresh fictional TEST business was created through the Windows app at the schema-40 `05f8d3b` build. Windows UI Automation used control patterns without desktop mouse or keyboard control. These are developer-machine workflow checks, not actual Surface/offline/second-device acceptance.

The app created two owners with distinct word passphrases, rejected an empty customer name, saved a name-only customer with its contact warning, added contact information, and saved a second same-name customer. Wrong financial confirmation was rejected. Alex approved a saved $39.00 expense they created; Morgan signed in and approved Alex's separate $11.47 draft. Restart retained the posted movement and owner lock returned to sign-in.

An independent read verifies database integrity/foreign keys, two active owners, two distinct customer IDs, correct draft creators/approvers, two posted expenses and four journal lines. Debits and credits both total 5,047 cents. The only photo-permission history is each customer's initial **Not asked** state. No document, ownership or opening fact was invented.

These checks exercise the developer-machine paths in AC-001, AC-002, AC-004, AC-005, AC-006 and part of AC-010/AC-044. The complete release scenarios still need their remaining actions and applicable device/account evidence. The separate full storage regression passes 1,639 cases in 7 minutes 58 seconds at `05f8d3b`.
