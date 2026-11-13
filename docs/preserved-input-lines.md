# Preserve stored multiline input during ordinary edits

Date: November 12, 2026. Application fix: `8dda1a7`. Schema remains 40.

The customer and document fixes exposed another native failure: loading a valid saved loan into single-line controls and changing only its name discarded the second lines of its terms and evidence reference. Separately, Windows normalized mixed CR, LF, and CRLF customer values on a name-only save. Words remained visible, but five stored values changed and an extra permission-history entry appeared.

The shared input factory now configures a multiline control before assigning an existing value that contains a line break. A small `Form.ReadInput` helper returns the original string when only newline representation differs; otherwise, it returns the edited text. Customer text fields, loan terms/evidence, and quote/invoice terms use that helper. [Microsoft's text-box guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/text-box) and [`AcceptsReturn` documentation](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.textbox.acceptsreturn?view=windows-app-sdk-2.0) describe the control behavior. The native before/after probes establish the storage effects here.

## Native failure and regression checks

All data is fictional, in isolated TEST businesses. Valid customer/loan fixtures are created through the actual business APIs with authenticated owners, not raw database writes. Photo permission remains Not asked.

| Check | Before | After |
| --- | --- | --- |
| Name-only loan edit | Terms and evidence each become first-line-only | Every property except name/revision matches its original value, including CR terms and LF evidence |
| Name-only customer edit with mixed newline styles | Five raw values change; permission history grows to two entries | Every other property is exact, including CR/LF/CRLF; one original permission entry remains |
| Deliberate loan terms edit | Positive control for the preservation helper | An added third line saves, revision advances once, and untouched LF evidence remains byte-for-byte exact |

The second owner restarts and reads the edited loan terms and complete unchanged evidence. Customer second-owner, unchanged-record/New, Keep editing, and Discard checks retain the exact stored customer and permission checkpoint. The long-document quote and invoice preparation probes each retain all 4,128 characters; independent checks retain all issued snapshots/PDF hashes, source coverage, milestones, and private-field exclusions. Integrity and foreign keys pass, with zero financial movements from these probes. Debug builds with zero warnings/errors.

## Scope

Existing multiline values are promoted automatically; this does not make every initially empty free-text field a multiline editor. Exact original newline preservation applies to the callers of `ReadInput`. Deliberately changed text uses the control's newline representation. Previously discarded text is not reconstructed. Physical device, Google, signing, and complete release acceptance remain open.
