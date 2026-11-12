# Verify the authorized Google account

Status: The internal account probe and bounded JSON response reader are implemented. Saving verified connection grants, desktop controls, real consent/account checks, and delivery follow separately. Tests use fictional responses only.

The probe calls the pinned Drive `about.get` endpoint with the bearer token in its Authorization header and an explicit field selection: authenticated permission identity, email, display name, and storage limit/usage. The default HTTP client disables redirects and cookies. The token does not enter query strings or error messages. Google documents `drive.file` as a supported scope for this operation.

Storage bytes use checked signed-64-bit parsing. Missing limits remain Unknown; reported usage above a known limit yields zero observed free space. These are point-in-time provider facts, including pooled or shared storage when applicable. They do not reserve upload capacity or establish unlimited free storage.

JSON responses are bounded to 2 MiB before parsing, including streams without Content-Length. A complete-request deadline covers both headers and body, with a maximum of sixty seconds. Explicit caller cancellation remains distinct. Fixed errors distinguish unavailable grants, quota, storage, access/API enablement, server/network failures, and malformed/oversized responses. Provider contents and inner exceptions are omitted. The reader clears its owned response buffers after projection.

Eighteen new account/transport cases pass alongside eleven consent, eight client-settings, and seven credential-file cases (44 targeted cases). They cover header-only bearer use, narrow projection, unknown/full capacity, status/reason errors, invalid/overflowed facts, declared/streamed bounds, malformed tokens, I/O privacy, cancellation, and a body stalled after headers. The stalled-body case first failed because only the caller's safety cancellation stopped it; the complete-operation deadline corrected that behavior.

References: Google's [about.get authorization](https://developers.google.com/workspace/drive/api/reference/rest/v3/about/get) and [About storage fields](https://developers.google.com/workspace/drive/api/reference/rest/v3/about). No real Google request, connected-app credential, upload, or success timestamp was used.
