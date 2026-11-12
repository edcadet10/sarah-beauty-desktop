# Enter phone and email together

Phone and email share a row when the customer editor has enough width. They stack when space is limited. Both retain the larger entry frames and their original labels. The same responsive grid arranges dashboard cards; its name now reflects that shared responsibility.

## Checks on November 12, 2026

Claim: pairing contact fields can reduce form height without clipping or clearing either value. Reject the change if a field extends outside the window, disappears, or loses its saved text during resize.

Native checks find paired phone/email fields at 1280- and 900-pixel window widths and stacked fields at 500 pixels. Both saved values remain correct and inside the window. Billing, private notes, sensitivities, and the displayed photo-permission state also remain available. An independent read-only check confirms all 14 expected customer values at revision 3 and the same two permission-history entries.

The renamed shared grid retains the dashboard's three/two/one-column behavior at 1280/900/500 pixels, exact amounts, review actions, and reconciliation shortcut. Financial history and issued PDF hashes remain unchanged. The Debug build has zero warnings/errors.

This change reduces one row where space permits. It does not guarantee that every form fits without scrolling. Physical touch/keyboard, larger text, and complete keyboard traversal remain separate acceptance checks.
