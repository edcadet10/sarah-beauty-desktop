# Keep saved-archive delivery pending

Status: The durable queue engine is implemented. Desktop polling and controls follow separately. Calling the engine once does not start a recurring worker.

The queue selects the highest-watermark completed local archive from this writable instance and current recovery recipient. It requires saved Drive output permission, coalesces already covered sources without a network request, and retains exact observed/delivered watermarks. Later business changes remain **Waiting for local archive** until a newer complete local archive exists. This component does not create that archive. Configure local scheduling or create one explicitly.

A separate queue lease prevents concurrent queue decisions while the worker retains its own delivery lease. Retry state survives restart, with fixed failure codes and one-, two-, four-, eight-, sixteen-, thirty-two-, then at most sixty-minute delays. The failure count is bounded. Changed policy/account/folder generations reset the earlier retry scope. Covered sources clear obsolete retry state without another upload. Disabled/invalid permission starts no Google request.

Queue observations, retry state, and delivery history do not advance the business watermark or renew a human session. The saved state is an observation; actual Running activity comes from the health service's active-worker registration. Earlier successful coverage remains separate from later pending work and errors.

Five cases pass using actual local encrypted archives and the simulated HTTP service: locked restart/coalescing with no extra network calls, persisted offline and quota delays, later changes waiting without reupload, queue-lease exclusion, unavailable copied sources, and withdrawn permission. Private provider diagnostics do not enter retry state. Windows Debug has no warnings/errors. No real Google delivery or second-device recovery occurred.
