# Preserve complete customer details during ordinary edits

Date: November 12, 2026. Fix: `e87b524`. Schema remains 40.

A native probe found that changing only a customer's name reduced six existing multiline fields to their first lines: billing address, makeup preferences, sensitivities, private notes, permission scope, and permission details. The changed scope/details also created an unintended extra permission-history entry. This reproduces the single-line initialization issue found in [document terms](multiline-document-terms.md).

Those six fields now use the shared `Form.MultilineInput`, which configures returns/wrapping before assigning saved text and bounds the control height. Names, contacts, and billing name retain their single-line controls. The optional groups, responsive contact layout, and customer edit protection remain in use. [Microsoft's text-box guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/text-box) describes multiline input and wrapping for longer text.

## Native before/after checks

Two separate fictional TEST businesses are created through the app. Each valid customer fixture is seeded once through `CustomerBook.Save`, using the actual owner/session and normal business validation, with six two-line values, contact/billing details, and Photo permission = Not asked. No consent is invented. No raw database writes are used for setup.

| Probe | Before | After |
| --- | --- | --- |
| Load all six multiline fields | All six lose the second line in the editor | All six match the complete saved text |
| Change only name; save through Customers | All six stored values become first-line-only | Every other customer property exactly matches the seed; only name/revision change |
| Permission-history rows after that save | Two, due to clipped scope/details | One original Not asked entry |

The second owner restarts and sees all six complete values. Complete customer/permission rows match the post-save checkpoint. Native unsaved-edit checks also pass: an unchanged multiline record opens New without a false warning; Keep editing retains an added unsaved line; Discard opens a new form without saving it. Stored rows remain unchanged after these probes.

Native 1280- and 500-pixel form captures were inspected, and resizing retains the complete notes. Debug builds with zero warnings/errors. The self-contained Windows x64 preview publishes successfully; the second owner reads all six complete fields from that published app, with unchanged stored rows. Both fixtures retain zero movements/journal lines, and integrity/foreign keys pass.

## Scope

This fixes ordinary customer editing for valid stored multiline details. Other sections' free-text fields, full keyboard/touch/larger-text behavior, physical devices, real Google delivery, signing, and complete release acceptance remain separate work. Existing shortened test data is retained as failure evidence; it is not presented as recovered automatically. The change does not reconstruct text already discarded by an earlier save.
