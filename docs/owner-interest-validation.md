# Validate a complete ownership confirmation

A draft contains an effective date, expected business revision, one exact fraction per owner, a confirmation/correction reason, and optional correction/evidence version identities. Blank fractions remain unknown and cannot be confirmed. Explicit zero is valid. Fractions must be between zero and one, use at most six decimal places (four decimal places when shown as percentages), and total exactly one. This precision is a settings contract and introduces no financial allocation.

Normalization copies and freezes the share list, canonicalizes identities, rejects duplicate owners, and trims the required reason. Database membership, inclusion of every owner, evidence, and correction/concurrency checks belong to the next transactional service step.

All seven Release cases pass for exact totals, explicit zero, caller-list mutation, read-only results, missing/out-of-range fractions, insufficient/excess totals, excessive precision, duplicate identities, empty reasons, invalid revision/date shapes, and malformed version identities. These tests perform no database confirmation or posting.
