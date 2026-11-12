# Capture one readable-export source

Status: The internal source-capture component is implemented. Allowlisted tables/reports, Sheets transport, durable generation publication, and desktop export controls follow separately. Capturing this source does not export or upload a workbook.

The source uses the existing verified SQLite backup API to capture committed WAL data into a private temporary standalone database. It opens that copy read-only, checks schema/business/store/instance identity, database integrity, foreign keys, retained source hashes, and exact balanced journal facts. A generation ID, source watermark, capture time, mode, and schema identify that source. Later local changes remain outside it.

A manual preview requires an active owner. Background capture requires the exact saved Sheets-output permit, checks it against the copied database and current live authority, and does not touch a human session. Drive-only permission cannot authorize a readable export. Cancellation is checked around database copy/validation boundaries; this component does not claim immediate interruption of synchronous SQLite copying.

The temporary database contains private local records and authentication hashes. It stays in the app's private staging directory and is never the readable payload. Future projections must select explicit public columns and omit device credentials, authentication material, private notes/sensitivities, and media binaries. No generic database/JSON dump is authorized by this component.

Disposal closes the read-only connection and removes only workspace files it owns. Failed capture also cleans those files. Unexpected files remain for review. Projection access rejects a disposed snapshot.

Seven cases pass: consistent earlier WAL facts while local writes continue; read-only enforcement; locked-session/watermark preservation; late withdrawal, cancellation, owner deactivation, and instance change; Drive-only rejection; and cleanup that preserves an unexpected file. No Google request occurred. The latest full suite has 837 passing cases before these seven additions.
