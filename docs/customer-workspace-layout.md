# Browse and edit customers

On a wider window, customer search and the list appear beside the editor. On a narrower window, the panels stack. Choose an existing customer to edit it, or select **New customer** to start a blank record. A new record clears the previous list selection so the highlighted row cannot imply that an existing customer is being edited.

The initial editor shows a prompt instead of an unrequested blank form. Existing validation and optional-field behavior remain unchanged.

## Checks on November 12, 2026

Claim: separate browser/editor panels can clarify the editing context without losing access on smaller windows. Reject the implementation if the wide panels overlap, narrow fields clip horizontally, resizing clears entered text, or a new record leaves the previous customer selected.

Native checks verify separate browser/editor columns at 1280 × 900 and aligned stacked panels at 900 and 500 pixels. Name fields remain within the window and retain an unsaved value across both resizes. New customer clears the prior selection; an empty name still rejects. No customer was created by these rejected or unsaved probes.

The existing identity checks also pass in the new layout: two same-name fictional customers retain distinct complete accessible references, references fit both tested widths, and selecting the archived customer opens its expected contact, archive state, and billing address. Financial history, cancellation records, current balances, and issued PDF hashes remain unchanged. The Debug build has zero warnings/errors, and the wide selected-customer render was inspected.

Keyboard traversal, larger Windows text, touch sizing, and actual Surface acceptance remain pending. This layout change does not add unsaved-change confirmation to customer selection or New customer; that protection remains separate work.
