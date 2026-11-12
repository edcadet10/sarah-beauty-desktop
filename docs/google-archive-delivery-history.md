# Retain Google archive delivery attempts

Status: Durable delivery and attempt records are implemented. The worker, queue, desktop controls, and real Google acceptance follow separately. Tests create real local encrypted archives and use fictional Google facts; no real Google upload occurs.

A delivery reserves a Drive file identity before content transfer. It binds a completed local archive's entire history fingerprint, current writable instance, recovery recipient, exact captured watermark/size/hash, connected account generation, and destination folder/binding. Repeated or concurrent reservations for the same source/destination reuse the earlier identity. A newly generated unused ID does not replace it.

Each try gets a separate attempt record with its policy revision, start/end time, status, and fixed failure code. Earlier Failed, Cancelled, or Interrupted attempts remain visible. Only one Running attempt can exist per delivery. A worker holding the exclusive delivery lease must mark earlier uncertain Running attempts Interrupted/CompletionUnconfirmed before starting another; this history service does not infer that another process stopped by itself.

Successful completion rechecks exact current output permission, connection, source fingerprint, current recovery material, and immutable attempt/result facts. Disabling blocks completion while still allowing its failure to be recorded. Later explicit permission for the same folder/account can reuse the delivery identity with a new attempt and policy revision. Changed/restored instances or replacement destinations cannot inherit earlier authority.

History changes do not advance the business watermark or touch human sessions. An active owner is required to read the public history screen. Exception messages, provider details, private upload URLs, and tokens are not retained; unknown error codes map to a fixed generic code.

Seven cases pass using actual local archive creation: durable identity/reopening with locked background access, unchanged watermark, retained failure/interruption/retry history, changed attempt rejection, withdrawn/regranted permission, changed source path/instance/recipient, and concurrent reservation/attempt exclusion. Remote response verification belongs to the separate transport component; history tests do not establish actual Google delivery or a restore test.
