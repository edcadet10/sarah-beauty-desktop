# Review Google archive coverage

Status: The archive-health service is implemented. Queue/runtime and desktop delivery controls follow separately. It reads saved facts without a Google request.

Health shows current business watermark, exact last verified archive watermark/time, latest attempt, and last successful attempt for the saved instance/account/folder/recovery recipient. A later failure, cancellation, disabled choice, or disconnected grant does not erase that scope's earlier success. A new folder or writable instance starts separate current coverage; older deliveries remain in history.

States distinguish Not configured, Disabled, Permission required, Recovery setup required, Never completed, Running, Needs review, Failed, Cancelled, Pending, and Succeeded. Running requires an attempt actually registered by this process's worker. Uncertain saved Running records need review. Pending means later local changes are beyond verified archive coverage; it does not claim they were uploaded.

Background reads do not touch human sessions or business watermarks. The public owner-review entry point requires an active local session. Last success records an earlier verification observation; health does not make a fresh remote existence/sharing check or establish a restore test.

Five cases pass: later changes and uncertain Running history with locked access preserved; retained success after failure/disable; new-folder coverage with history retained; copied-instance isolation; and active-worker completion/disconnect. Tests use real local encrypted archives and fictional HTTP responses. No real Google acceptance occurred.
