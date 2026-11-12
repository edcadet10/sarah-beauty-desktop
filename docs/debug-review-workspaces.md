# Isolate fictional native reviews from working data

Debug builds may use `SARAHBEAUTY_REVIEW_DIRECTORY` to resolve separate `Test` and `Live` children under an explicit absolute review root. The root cannot overlap normal TEST/LIVE data or an explicit TEST preview. Ancestor and selected paths reject links/reparse points. Resolution creates no folders or stores.

Release path routing does not read this variable. The existing TEST-only preview override remains scoped to TEST. This development setting supports fictional native checks without initializing the normal LIVE business directory.

Twenty-two review/existing-mode path cases pass. Direct probes of the actual compiled Debug and Release assemblies confirm their different routing, unchanged Release TEST preview behavior and no folder creation. Windows Debug passes with zero warnings/errors.

A fresh native Debug launch used the isolated TEST review folder, showed owner setup, created no LIVE folder and closed gracefully. Independent read-only SQLite checks confirm schema 39, TEST mode, integrity/foreign keys and zero business, owner, customer, money or document entries. No actual owner's business was created.

The parent full creation/opening source passes 1,477 local and hosted cases. Full mode-aware activation regression is running. Native mode selection/creation/recovery wiring and release acceptance remain separate work.
