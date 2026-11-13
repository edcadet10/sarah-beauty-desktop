# Run readable export scheduling while the app is open

The desktop starts the Sheets queue before owner sign-in and checks saved permission/profile state every 15 seconds. Scheduled delivery uses the service's current authorization and profile checks without renewing owner access. Closing, switching stores, or retiring an instance cancels its loop; window shutdown waits for all local, archive, Sheets, and registered foreground operations to finish stopping.

Backup health now displays actual selected-report coverage and Sheets runtime state instead of a permanent never-completed label. Unconfigured or disabled Sheets consent reports an off state and creates no job or HTTP request. The loop operates only while the app is running and awake; it is not a Windows background service.

The native fictional TEST fixture passed sign-in, owner lock, signed-out close, restart, and signed-in close with all runtime shutdown waits completing. Read-only checks verified unchanged financial/document/evidence rows, balanced 26,000-cent journal sides, three managed file hashes, no Google connection/token/output permission, and no Sheets job/profile/queue/cache. The fixture has a fake client import only; no Connect action was performed.

The full Release suite through automatic queue passes all 1,222 cases in 5 minutes 2 seconds. An additional targeted Release case checks the off-state label/no-HTTP path. Windows Debug has zero warnings/errors. These tests do not establish real Google delivery, physical failure, a second device, or Surface acceptance. Preview, desktop controls, retention, and the remaining full build continue.
