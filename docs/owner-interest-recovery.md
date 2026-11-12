# Verify ownership when capturing and restoring backups

Schema-33 recovery checks now validate every business's ownership confirmations using the same strict history reader. This checks hashes, source identity, share copies, revisions/corrections, and retained evidence metadata bindings. Earlier schema versions keep their existing recovery contract. Managed file verification still checks the complete archive file inventory and byte hashes separately.

Three new checks failed before integration: archive capture accepted changed ownership JSON, archive capture accepted changed relational shares, and the reader accepted a rehashed confirmation claiming a different evidence metadata hash. All three now reject those sources. Eighteen related Release cases pass across recovery, confirmation, and history.

The fourth recovery case creates an encrypted archive containing a real fictional posted receipt and managed PDF-backed ownership confirmation, changes the active ownership afterward, stages the earlier archive, and prepares a fresh writable instance with reviewed access recovery. The recovered confirmation/share set, original evidence hash and readable bytes, and journal total remain captured; the later correction stays only in the source. This is local cryptographic recovery with fictional stores, not a second-device or physical-failure rehearsal.
