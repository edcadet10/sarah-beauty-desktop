# Publish and independently check the remote file state

After source and status readback, the worker can promote a private job-bound staging file to verified. The transport checks full private file bindings, patches only the app's export-state property, checks the returned file ID, then independently rereads all required bindings, permissions, and the verified state.

An already verified file needs no patch. This supports retry after an applied update whose response was lost. Neither an HTTP success nor the app property proves that cells are correct; the caller must finish source/status/schema readback and current local permission checks.

The request uses Drive's documented [metadata PATCH semantics](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/update) and [custom file properties](https://developers.google.com/workspace/drive/api/guides/properties). It does not change workbook values, titles, parents, or permissions.

Six simulated HTTP cases pass in Debug and all 15 related publication/attempt cases pass in Release for normal/lost responses with one patch across retry, fresh access per request, sharing before/after the update, unchanged remote state, and a wrong acknowledgment ID. The complete suite through publication checkpoint passes all 1,144 cases. These are controlled responses, not real Google acceptance. The complete delivery worker, pacing, scheduling, and controls remain subsequent work.
