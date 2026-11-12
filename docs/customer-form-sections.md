# Enter customer details

Keep the customer's name, phone, email, and preferred contact visible. Open Billing details, Preferences and private notes, or Photo permission details for the related optional fields. The current photo-permission choice remains visible as separate text. **Save customer** is the primary action.

Closing a section does not clear its fields. Saving a name or contact change preserves the optional values already loaded for that customer. Recording a fictional permission choice in the test fixture does not establish any real customer's consent.

## Checks on November 12, 2026

Claim: grouping optional fields can shorten the initial form without losing customer details. Reject the change if a closed section drops a value, a decision history entry is duplicated, or the other owner cannot reopen the saved details.

A fresh fictional native fixture rejects an empty name and retains the name-only contact warning. All grouped fields save and reload. The other owner then changes the name with optional sections closed. Two restarts retain every checked value. An independent read-only database check confirms 14 expected customer values at revision 3, exactly two permission-history entries, database integrity, and valid foreign keys. The Debug build has zero warnings/errors.

The first dynamic photo-section header became unavailable under its new name in native UI Automation. The current design keeps the expander label stable and exposes the selected permission in a separate text element; the native check now finds the updated state.

This change does not alter customer validation, permission rules, or storage. Wider browsing/editor layout, full keyboard traversal, larger text, and physical touch acceptance remain separate work.
