# Review voluntary lock and close

The downloaded `6b753ca` preview clears cached customer and Money fields immediately when the owner selects Lock / switch owner or closes the window. Native before probes reproduce both losses without saving, approving or changing any retained business table.

## Keep working or explicitly exit

Voluntary lock and signed-in window close now show a shared review. It explains that unfinished fields in every section will be cleared and saved records retained. **Keep working** is the default; the explicit actions are **Lock now** and **Close app**. Locking never requires a credential. Another open review dialog keeps control rather than being replaced by a second exit prompt.

Cancelling close happens before backups or registered operations are stopped. Confirmed close retains the existing shutdown sequence. Repeated close requests do not stack prompts. Sign-in/setup screens can close without an owner-workspace prompt.

Automatic expiry, changed credentials and other security lock paths do not wait for permission. They still revoke owner access and hide its content immediately. They also dismiss an open exit review. The sign-in reason states that unsaved fields were cleared. No passwords, PINs or unfinished fields are written to a draft file by this change.

This conservative review appears for a signed-in workspace even if its fields are already saved; it does not claim to detect every editor's dirty state. Windows shutdown, forced process termination and power loss cannot be turned into a guaranteed interactive review.

## Try to lose cached fields

The claim is that Keep working retains cached customer/Money fields, including invalid raw input, and leaves the session and saved facts usable. Losing a field, stopping operations after cancellation, replacing another modal, or changing a saved row rejects the claim.

- The original downloaded app reproduces both silent losses. The initial harness wrongly expected a new customer editor to exist before New customer, then encountered an unavailable startup window; neither failed harness run is counted as a product pass. The corrected probe waits for owner sign-in and opens the editor explicitly.
- Fixed native probes retain both cached drafts after cancelling manual lock and window close, including a second close request. Closing while Money's discard review is open leaves that review in control. Its Keep action retains the raw invalid amount.
- Explicit lock clears the unfinished fields, permits password sign-in and opens fresh editors. Explicit close exits normally; a different owner restarts and reads the saved records. All saved tables match the prior posted checkpoint, with database integrity and foreign keys valid.
- Windows Debug builds with zero warnings/errors. All six owner-activity and registered-shutdown regression cases pass. These checks establish the stated boundaries, not every form, physical device or power-failure outcome.
- An actual idle probe expires after 905 seconds while the exit dialog is open. Owner content and the dialog disappear without confirmation; the other owner signs in normally, and every saved business table remains exact.
- The [GitHub exit build](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43432004423/artifacts/14040063510) matches all 620 paths, sizes and hashes. Downloaded-app cancellation, cached-field, competing-dialog, explicit-exit and second-owner probes pass with all saved tables retained. Its [complete hosted check](https://github.com/edcadet10/sarah-beauty-desktop/actions/runs/43432007200) passes all 1,684 tests, recovery interoperability and Windows Debug with zero warnings/errors.

Microsoft documents [AppWindow.Closing](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.closing) as a system-affordance close event, and [ContentDialog.DefaultButton](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog.defaultbutton) as the default focus/Enter action. The implementation cancels the initial window event and defaults the review to Keep working.
