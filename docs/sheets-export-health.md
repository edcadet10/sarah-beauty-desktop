# Read export coverage and the last delivery outcome

Health keeps the current business watermark, latest attempt, highest-watermark verified generation, and verified generation for the selected report dates separate. A successful export for different dates does not cover the selected reports. Later desktop changes remain pending, and a changed destination cannot inherit earlier folder coverage.

Succeeded outcomes require matching creation and publication file identities. A status string alone cannot advertise coverage. A persisted Running attempt without an active worker needs review. Disabled or unavailable permission remains distinct from historical verified results.

These are local observations from delivery history. They do not inspect Google's current contents/sharing or establish that a past workbook remains unchanged. Public health reads require an owner session; background reads do not renew a session or change business data.

Four new Debug cases pass for selected-date/highest-watermark separation, later changes, locked-owner/background behavior, persisted versus active status, failures/disable, destination replacement, retained history, and a forged success status without file/publication proof. Tests supply fictional local completion results; complete-worker transport tests provide separate simulated delivery evidence. The full Release suite through the worker is running. Queue/backoff, scheduling, controls, and the remaining full build continue.
