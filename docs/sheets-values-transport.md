# Send literal values and verify typed grid reads

The Sheets value transport sends only planned RAW payloads to a fixed HTTPS workbook endpoint. It freezes rectangle inputs, rebuilds the canonical JSON, and requires exact agreement with the planned bytes before obtaining access. Requests remain within the 1,500,000-byte body limit. Owned copied payload buffers are cleared after use, including rejected mismatches.

Typed read requests are reconstructed from the workbook/range/field-mask definition. A different supplied URI or response estimate is rejected before access or HTTP. Reads request uncached responses and run the complete workbook/sheet/offset/coverage/literal verifier. Both operations invoke the supplied access callback immediately before HTTP and recheck cancellation afterward. The default shared client disables redirects/cookies and bounds response bodies and deadlines.

A successful update response confirms only the expected workbook identity. It produces no generation-completion proof. Every required cell must pass independent readback, and the worker must still verify privacy, source/permission fences, full schema, and publication state. This transport does not implement those worker responsibilities or retries/pacing.

Nine new controlled HTTP cases and six planner cases pass in Release (15 targeted). They cover actual RAW request JSON and independently returned typed cells, one fresh access check per request, bearer headers without credentials in URLs, canonical-body/URI rejection before access, denied access, cancellation after access, wrong workbook, quota/revocation, redirect responses, oversized bodies, and a successful write acknowledgment followed by changed readback content. Errors do not expose provider diagnostics or cell contents.

All HTTP responses are simulated. No real Google request, consent, upload, or readable publication occurred. Private workbook creation/schema, durable jobs/worker/completion, scheduling/UI, and real account/device acceptance remain unfinished.
