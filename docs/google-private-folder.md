# Verify a private Google output folder

Status: The internal folder creation and verification component is implemented. Owner-reviewed destination settings and business-data delivery follow separately. Tests use fictional HTTP responses; no Google folder was created.

The component creates an app-tagged folder in the connected account's My Drive root. It sends only a generic TEST/LIVE folder name, folder MIME type, and random output binding. It requests `ignoreDefaultVisibility=true` to bypass domain defaults and creates no sharing permissions or invitations. Google's [files.create reference](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/create) states that parent permissions still apply, so a successful creation alone is insufficient evidence of privacy.

A separate metadata read must match the requested file identity, folder type, output binding, and app purpose. It requires the connected account as the sole owner and sole user/owner permission, `ownedByMe=true`, `shared=false`, `trashed=false`, no shared-drive identity, and permission to add children. Missing facts, another user/group/domain/public permission, an unexpected owner, or a changed binding cannot produce a verified destination. The [file resource](https://developers.google.com/workspace/drive/api/reference/rest/v3/files) documents ownership, permission, sharing, and capability fields.

Requests use the existing bounded, cancellable Google JSON transport and bearer-header authentication. Validated file identities cannot add path/query components. Fixed errors omit tokens and provider response details. The component writes no local consent, job, audit, or business records and cannot enable output by itself.

Eleven cases pass: request shape and independent post-create verification, shared/group/domain/foreign-owner/shared-drive/trashed/binding/read-only/type rejection, missing permission facts, path injection, and malformed bearer input. The group/domain probes keep `shared=false` to verify that the permission list is checked independently.

Privacy is an observation, not a permanent lock on Google's sharing state. Delivery must recheck saved authority and the remote destination. Failed or interrupted creation can leave an empty Google folder; this component does not delete remote objects or send business records. Real account acceptance remains pending.
