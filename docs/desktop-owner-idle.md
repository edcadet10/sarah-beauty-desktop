# Lock the owner workspace after inactivity

The desktop checks current owner access every five seconds without renewing it. Key, pointer-press, and wheel input use the throttled activity adapter; programmatic window activation only checks access. Pointer activation counts as user input. Handled input is observed without changing the control's event handling.

Expiry, revoked owner access, or manual lock removes the workspace before building sign-in. Open XAML dialogs and popups are dismissed. Manual lock keeps the current store's automatic runtimes running; a new sign-in creates new access. Window shutdown stops activity checks, locks the session, dismisses owner UI, and waits for background/registered operations.

The event setup follows Microsoft's [routed-event registration](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.addhandler) and [window activation](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.window.activated) behavior.

Windows Debug builds with zero warnings/errors. The native fictional manual-lock walkthrough removed workspace navigation, displayed the lock reason, accepted fresh sign-in, and closed cleanly. A real-time idle probe started on November 12, 2026 at 10:52:01 UTC with a confirmation dialog open. Read-only UI observations sent no input or protected refresh. The workspace/dialog remained visible at 871 seconds; sign-in appeared at 901 seconds. The expiry reason was present, workspace navigation and the dialog were absent, fresh sign-in succeeded, and shutdown completed without forced termination. The ignored local transcript retains the actual result.

Read-only checks before and after the probe preserve the existing financial/document/evidence fixture, balanced journal, three file hashes, watermark 33, schema 32, and no Google connection/output settings. This is a fictional native Windows desktop check. Gesture/touch, native file-picker interactions, suspend/resume, unsaved-draft recovery, and complete release acceptance remain pending.
