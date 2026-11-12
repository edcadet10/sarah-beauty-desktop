# Initialize and independently verify the staging schema

The schema transport first matches the layout's generation/digest with the job's file specification, then independently verifies that the existing file is private, editable, app-bound, and still staging. It reads the actual sheet layout before producing initialization requests. A matching complete schema needs no mutation.

Before an update, it rechecks file privacy/state and invokes the supplied current-access callback again. After the update acknowledgment it independently rereads and verifies the full layout, then checks file privacy/state once more. Fixed endpoints, bounded responses/deadlines, canonical schema fields, cancellation, and cleared owned payload copies use the shared transport boundaries. The caller still must enforce current source/output policy and folder privacy through its access fence.

An uncertain update is not automatically resent. A later attempt inspects the same job file; an already applied complete layout becomes a no-op. This depends on a stable durably recorded file ID, not a new file creation attempt.

Seven controlled HTTP cases pass in Debug. The simulated server applies the actual schema request JSON using documented index behavior. Normal initialization and lost-response retry reach the exact layout with one mutation. Source mismatch, privacy changes before/after work, withdrawn access before update, and changed independent readback all fail. Access callbacks run for every request.

No real Google workbook changed. Durable jobs/creation checkpoints, complete data worker/readback/status publication, pacing/retries, scheduling/UI, and real account/device acceptance remain unfinished.
