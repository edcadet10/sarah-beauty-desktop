# Persist immutable readable export job sources

Versioned job records retain the business/store/instance, Google connection and folder binding, generation ID, independently trusted manifest digest, mode/schema/watermark/capture time, date selection, and creation time. They use a business-scoped `AppMetadata` prefix, following existing delivery bookkeeping. They contain no token or recovery secret and do not change the business watermark.

Readers validate the serialized record against its exact metadata key and supported identity, range, mode, schema, digest, and UTC timestamp rules. Older supported schema numbers can remain in history; this does not authorize running an incompatible source. A job yields its local artifact reference and remote file specification without reconstructing source data or a human session.

The date-selection record is now a public immutable DTO for the upcoming export controls. Validation still runs at business/export boundaries.

Ten new Debug cases and four assembly cases pass in Release (14 targeted). They cover reopen persistence, locked-owner expiry and watermark preservation, foreign-business history isolation, and rejected corrupted record versions/keys/generations/digests/ranges/timestamps/schemas/modes/watermarks. A redundant fixture generation build was removed before the Release run.

These are validated source records, not authorized reservations or completed jobs. Permission/source-checked reservation, creation/attempt checkpoints, full worker/completion, scheduling/UI, and real account/device acceptance remain unfinished. No Google request occurred.
