# Keep customer edits when changing records

Choosing another customer or **New customer** checks for unsaved changes. **Keep editing** retains the fields and prior selection. **Discard changes** opens the requested record or a blank new form. Returning fields to their original values avoids a needless prompt. Switching to another workspace section keeps the cached customer draft.

While a customer save is running, the customer browser and editor are disabled. A successful save selects its saved record and restores the controls. A rejected save restores the controls and clears the saving message. This protection is scoped to the customer view; global reload, lock, shutdown, and other sections remain separate work.

## Checks on November 12, 2026

Claim: switching records must not silently erase unsaved customer changes or allow another record operation midway through a save. Reject the change if Keep editing loses a field or changes the selected customer, Discard opens a different target, cached navigation clears the draft, or a delayed save leaves record controls active.

Native checks exercise Keep editing and Discard for existing-record switching, New customer, and leaving an unsaved new draft. The prior selection and fields remain correct. Section navigation retains the draft; undoing the field change avoids a prompt. Every original customer and permission row remains byte-identical, and financial history and issued PDF hashes remain unchanged.

A separate fresh fictional fixture holds an actual SQLite writer lock for eight seconds. Search, list selection, New customer, and name entry remain disabled. Leaving and returning to the cached section cannot bypass the guard. After release, one save completes and the saved customer is selected. An independent read-only check confirms exactly one customer at revision 1, one Not asked permission entry, database integrity, and valid foreign keys.

An empty-name rejection also restores controls and removes the saving message. The Debug build has zero warnings/errors. The native driver waits for dialog dismissal before attempting the next action; its first sequence attempted another selection during the closing transition and was ignored without losing data.

This does not complete application-wide unsaved-change or busy-operation acceptance. Keyboard, larger-text, Windows contrast-theme, physical Surface, and complete release walkthrough checks remain pending.
