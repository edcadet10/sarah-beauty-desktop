# Bind a private workbook to its export job

Workbook creation metadata contains only a generic TEST/LIVE name, the native spreadsheet MIME type, one parent folder, and opaque app properties. The properties bind purpose, output-folder identity, job, generation, manifest digest, mode, and staging state. Metadata grants no permissions and supplies no pregenerated file ID.

Verification reuses the shared Drive owner-only checks, then requires edit capability, the exact single parent, every app binding, and a supported state. Group/domain/additional permissions are rejected independently of the `shared` flag. The default required state is **staging**; a verified workbook requires an explicit verified-state observation and is not an ordinary write destination.

Twenty new Debug cases pass for generic metadata, staging/verified state behavior, wrong identity/MIME, sharing/extra permissions/ownership, shared Drive/trash, wrong or multiple parents, mismatched folder/job/generation/digest/mode/purpose, missing edit capability, and unknown state. Fixed errors contain no actual file/account IDs. Observations describe the checked response at that time; they are not permanent privacy or access guarantees.

These are controlled metadata fixtures. No workbook was created or inspected on Google. The HTTP file service, uncertain-create handling, schema transport, durable jobs/worker/completion, scheduling/UI, and real account/device acceptance remain unfinished. The worker must recheck file/folder privacy and current output/source permission before sending readable records.
