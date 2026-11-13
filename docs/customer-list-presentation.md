# Recognize a customer in the list

Customer rows show the name first, contact information or its missing-contact warning next, and the complete reference in smaller secondary text. Long lines wrap. The accessible row name retains the full reference so people with matching names and contact details remain distinguishable.

The list still selects its customer data item and uses a data template; it does not build a separate control for every search result in advance.

## Checks on November 12, 2026

Claim: clearer text hierarchy must preserve customer identity. Reject the change if a name or complete reference disappears, two same-name accessible labels become identical, or a row opens a different customer.

Native checks find two same-name fictional customers with distinct complete accessible references and visible name/reference bindings. Selecting the archived customer opens its expected email, archived state, and billing address. Full references remain within the window at 1280 and 500 pixels. Original fictional financial history, balances, cancellation records, and issued PDF hashes remain unchanged. The Debug build has zero warnings/errors.

This is a presentation change. Wider browse/editor layout, owner task testing, keyboard traversal, larger text, and physical touch acceptance remain separate work.
