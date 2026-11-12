# Keep a locked owner session locked

Passphrase confirmation checks the original session before and after credential verification. A manual lock while verification is running therefore rejects confirmation instead of renewing the old session. The temporary session used for the credential check is locked before leaving confirmation.

Session renewal and locking synchronize access to the expiry field. Lock is terminal for that session object: later renewal throws, even if it started after an earlier access check. Fresh sign-in creates a separate session; it does not revive the old one.

## Reproduce and check the correction

The existing credential-read hook forced a lock during the actual public confirmation path. That regression and a direct locked-session renewal case both failed before the change because neither threw. All nine related Release cases pass afterward, including four new cases for the reproduced race, direct renewal rejection, fresh sign-in, and normal confirmation. Existing credential/lockout concurrency checks also pass.

This change does not implement the complete idle-lock interface, credential changes/recovery, or all foreground cancellation rules. Those remain separate work. It changes no password material, business records, or Google output permissions.
