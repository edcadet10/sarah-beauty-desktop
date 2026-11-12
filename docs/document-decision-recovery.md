# Verify retained document decisions during recovery

Schema-36 capture, staged restore, and recovered-copy checks now use the strict decision reader for every business. Decision digests, retry payload hashes, exact source/kind/date/state, owner/evidence membership, and original document mode must agree. Earlier archive schemas keep their supported validation contract and migrate through the reviewed recovery flow.

Four checks failed before integration: changed decision JSON, changed retry payload hash, a contradictory current document status, and a rehashed document/decision pair whose LIVE label contradicted the TEST store. They now all reject capture. Fifteen related Release cases pass across decision recovery/history and upgraded recovery inventory.

The fifth new case creates an actual declined quote, original rendered PDF, managed evidence, and posted 12,345-cent receipt, then encrypts a full archive. A later proposal stays in the active source. The archived copy stages, prepares fresh owner access, and activates a new writable instance with the exact prior decision/version/hash, original PDF bytes and evidence, unchanged journal totals, and no reissuance. This is local fictional cryptographic recovery and activation, not second-device or full-release acceptance.

The first complete schema-36 run passed 1,321 of 1,323 cases. Two older ownership-migration fixtures retained the new decision table after removing its owner-scope index, so their emulated schema-32 layout failed relationship checks before the intended upgrade. The fixtures now remove only confirmed-empty newer decision storage; all four affected ownership-schema cases pass. The complete suite is being rerun after that fixture correction.
