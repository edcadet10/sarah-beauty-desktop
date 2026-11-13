# Keep unfinished Money fields

In the downloaded `6cd916a` preview, Prepare movement and selecting a saved Money record each silently replace an unfinished form. Native probes reproduce both losses with an invalid amount and without saving or approving a draft. All saved fixture tables remain exact.

## Confirm replacement and finish captured writes

Money now offers Keep editing / Discard changes before preparing another movement or showing a different saved record. Keep restores the original editor, selected row and movement context. Cached navigation retains the draft. A small field-state reader compares raw editable text, full selected record identities and checkbox values, so invalid input remains dirty and exact reverted input becomes clean. Search queries are filters rather than financial fields. Password fields are excluded.

The reader follows the form's logical panels/content, including collapsed content. Microsoft's [Expander API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.expander) identifies Expander as a ContentControl. The helper is used only by Money; it does not establish draft coverage for every other view.

Money controls remain disabled while the captured draft save or approval runs. The financial operations, captured values, revision checks and owner authority remain unchanged. Successful completion updates the saved review or posted result. Failed validation retains the original editable input.

## Validate the loss and write boundaries

The claim is that these replacement actions preserve unfinished Money fields after Keep, and that a busy write cannot be redirected by changing the editor. Lost input, a false dirty result after exact reversion, altered saved facts during cancellation, or duplicate/redirected posting rejects the claim.

- Original native probes reproduce both silent replacements. Fixed probes pass Prepare/record Keep and Discard, invalid rejected-save retention, cached navigation, exact field reversion, account/checkbox changes and search-only controls.
- Two separately saved, unapproved fictional drafts verify row identity: Keep restores A's selection and invalid edits; Discard opens B. Every stored table matches its pre-probe checkpoint.
- A held SQLite writer disables the amount/name, movement, Prepare and save controls. Releasing it completes one B save. Only B's name/revision change; A remains exact, and no movement or journal is created.
- A separate held writer disables movement, Prepare and approval. Releasing it approves B once as the authenticated second owner. Independent records retain A, B revision 3/Posted, one $2 movement, two journal lines balanced at 200 cents per side, and the second owner's approval audit. Restart reopens the same posted record with valid database integrity/foreign keys.

Debug builds with zero warnings/errors and the self-contained Windows publish succeeds, including notices for all 29 runtime packages. Packaged/downloaded-app checks remain separate evidence. Global lock/shutdown behavior and broader release/device acceptance remain open.
