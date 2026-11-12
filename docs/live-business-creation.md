# Create a separate clean LIVE business

The creation service requires explicit USD currency, business time zone, books start date, new-business-only acknowledgment, two distinct owner names, each owner's passphrase, and a private creation reason. It validates these inputs before creating folders. Initial evidence cannot refer to files from another business.

Creation writes a new LIVE database in a unique pending folder. It records both owners, the reviewed initial profile and the default chart, then publishes that complete folder as `Primary`. Store, instance, business and owner identities are generated independently. No TEST source is accepted or copied. Legal/review facts, ownership and opening balances remain unknown unless explicitly supplied; default accounts have unconfirmed openings and no posted entries.

An existing `Primary`, including an empty directory or file, is preserved. Publication rechecks the destination and rejects links/reparse points. Failure or cancellation before publication retains the unfinished pending folder for review and leaves LIVE unselected. A competing creation can publish only one destination. Do not manually select or copy a pending folder as the working business.

Forty-nine creation, policy and owner-access cases pass. They cover unchanged TEST bytes with existing money, independent identities and sessions, preserved unknowns, no imported entries, reopen/sign-in, invalid inputs without folders, exact cancellation/failure types, a destination appearing during setup, competing creators and rejection of foreign evidence. Windows Debug passes with zero warnings/errors. Native TEST profile reopen/lock/close and independent database/file checks pass unchanged.

This increment provides the creation service. Existing LIVE selection/opening, native creation review, recovery activation, external delivery and release acceptance follow separately. It does not create an actual owner's business or verify legal/tax facts.
