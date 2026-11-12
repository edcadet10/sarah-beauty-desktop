# Create and locate the job's native workbook

The file service creates an empty native Google spreadsheet using metadata only. It sets `ignoreDefaultVisibility=true` and returns the new file ID before a separate privacy verification. The caller must durably record that ID and creation intent/outcome. No financial/customer values are included in creation metadata.

Domain-default visibility is bypassed, but [parent permissions still apply](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/create). Folder and file privacy must therefore be independently checked before readable writes. Verification checks the exact connected owner, capability, parent, app/job/generation/digest/mode/state bindings.

Google does not support pregenerated IDs for native Sheets creation; see [pregenerated file IDs](https://developers.google.com/workspace/drive/api/guides/manage-uploads#pre-generated-ids). The helper never automatically retries creation. A separate job-and-folder lookup returns at most one fully verified match; duplicate matches, a continuation page, or [incomplete search](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/list) fail as unconfirmed. A zero-result observation does not prove that an earlier uncertain create failed and must not authorize a blind repeat.

Ten controlled HTTP cases pass in Release; these plus file-spec/initialization checks pass all 34 targeted cases. They cover generic/native/no-ID creation, separate verification, current access callbacks, zero/one/verified/multiple/paged/incomplete/misbound lookup results, a lost create response followed by job lookup without another create, invalid IDs, denied access, and cancellation after access. All requests use the fixed Drive endpoint, bearer headers, bounded shared HTTP handling, and cancellation fences.

No real Google file was created, searched, or inspected. Durable creation intent/file-ID checkpoints, schema transport, worker/privacy fencing, completion publication, scheduling/UI, and real account/device acceptance remain unfinished.
