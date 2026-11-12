# Project asset positions, journal, and events

Four source-bound tables retain asset register facts at report capture, book-value roll-forwards and disposal as of the report date, attributed journal sides, events, and explicit detail coverage. Report IDs join ledger report date/mode/observation scope. Historical opening entries stay visible with `InSelectedPeriod=false`; events can remain meaningful when they have no journal lines.

Registered cost, tax basis, and approved carrying value remain separate. Unknown book values stay blank, while confirmed zero remains `0`. Archived register facts stay visible. Current reports reuse the existing asset calculation. Retained close snapshots keep their original facts after later register reviews; missing older snapshots are marked `Not retained`, and unsupported versions stop projection. Evidence inputs and raw snapshots stay out; public movement descriptions remain readable.

Two table cases plus the two asset kernel cases pass in Release. They cover unknown book value beside registered zero, actual full cost recovery and zero retirement with an event but no invented journal, locked access, and retained unknown register values beside a later reviewed cost/basis. Latest complete suite remains 918 before these asset additions. Other report groups and verified Sheets publication remain pending.
