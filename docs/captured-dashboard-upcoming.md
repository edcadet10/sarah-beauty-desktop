# Read captured upcoming-event datasets

The upcoming dashboard, reservation review, and paid-milestone queries now have internal business-ID paths with one supplied timestamp. Current confirmed wedding selection, agreement/acceptance review, deposit checks, artist state, separate undated work, known/unknown value counts, and planning-window limits use the existing calculations. Public calls retain their read hook and check owner access before returning.

Captured callers do not create or renew human access. Later cancellation stays outside the source. Agreement gross is a planning commitment, not cash income; an earlier as-of date before acceptance keeps its value unknown. Monthly known value remains a subtotal of known amounts alongside an explicit unknown-value count. Raw planning snapshots remain internal until selected scalar projection.

Two new cases plus twenty upcoming/reservation/billing cases pass in Release (22 targeted). They use an actually approved deposit and confirmed fictional booking, compare the whole captured result with the human view after later cancellation/lock, exclude private work notes, preserve unknown early acceptance value, reject late public lock, and enforce planning/business boundaries. Business Debug has zero warnings/errors. Latest complete suite remains 943 before dashboard additions. Projection follows separately.
