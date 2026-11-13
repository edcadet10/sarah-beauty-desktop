# Project retained document decisions

Two allowlisted tables describe verified quote/invoice decisions from the captured database. **Document decisions** preserves decision, business, document, version and retry identities; action/date; prior/result states; and recording owner/time. **Document decision coverage** states the captured-history scope and the fields intentionally excluded.

The decision tables omit private reasons, supporting-file descriptors/paths/identifiers/hashes, and raw snapshots. Join the exact VersionId to the issued document table; do not replace historical state with a current label or infer a cash refund from a void. Earlier retained reports keep their own original facts.

Two Release cases pass: a later invoice void and owner lock leave the captured table digests unchanged, private sentinels do not appear in the output, empty history invents no status, and a Thai locale still produces Gregorian ISO decision dates. This isolated projection is not yet part of the complete generation or Google workbook schema; that integration is the next focused change.
