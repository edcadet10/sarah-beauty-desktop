# Verify a completed Google archive

Status: The internal remote verification component is implemented. Durable delivery history and desktop controls follow separately. Tests use fictional HTTP responses; no real archive was uploaded or downloaded.

An authenticated metadata read checks the saved Drive file identity, exact byte length, SHA-256, parent folder, app purpose, output binding, and archive job/hash tags. The file must remain a downloadable binary file, owned only by the connected account, unshared, untrashed, and outside a shared drive. Folder and file verification share the same ownership/permission check. The [Drive file resource](https://developers.google.com/workspace/drive/api/reference/rest/v3/files) documents these fields.

A successful read can confirm an earlier uncertain completion without sending content again. A 404 returns an unavailable result; it does not prove global absence because Drive can hide inaccessible files. Other access/network/quota failures remain visible. A 409 reports that the file identity already exists and requires verification; it does not overwrite it. Durable callers must reuse the saved identity when retrying and preserve uncertain attempts.

Thirteen new cases and related transport cases pass (60 targeted cases). They cover exact completion without content writes; changed parent/binding/job/hash; group access even with shared=false; changed ownership, trash/download status, and missing permission facts; and distinct 404/403/409 handling. The last complete regression suite has 797 passing cases before these thirteen additions.

Remote checksum and privacy are observations, not a restore test or a permanent sharing lock. A delivery service must recheck current local permission, destination privacy, and completion authority before recording success. Second-device recovery and real Google acceptance remain separate work.
