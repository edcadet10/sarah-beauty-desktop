# Project opening and period-close history

Four source-bound tables retain opening confirmations/revocations, optional original/reversal movement links, closed period approval/source hashes and observed report scope, reopen provenance, and frozen reconciliation dependencies. Confirmed zero without a movement remains distinct from an unknown opening. Revocation and reversal histories remain separate from current account flags.

Retained report dates/mode/watermark must match the recorded period and captured source bounds. Original report watermarks/times remain visible after reopening and later transactions. Free-form opening/revocation/close/reopen reasons and evidence stay in the desktop and encrypted archive. Full retained reports are projected separately.

Two fictional cases pass for actual zero revocation/nonzero opening reversal and actual close/reopen with reconciliation dependencies after a later transaction. The first fixture attempted to post inside a still-reconciled month; existing safeguards rejected it. The fixture now uses February 1 without weakening reconciliation rules.

The complete Release suite before these two additions passes all 896 cases in 2 minutes 35 seconds, including the merged pointer-reader scheduling correction and prior export projections. Windows Debug builds with zero warnings/errors. Sheets publication and full release acceptance remain pending.
