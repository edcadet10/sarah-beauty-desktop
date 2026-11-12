# Project tax amounts and filing history

Four source-bound tables retain tax amounts, filing periods/status at capture, ordered filing decisions with captured owner names, and explicit filing-detail coverage. Report IDs join ledger report dates/mode/watermark/time. Current reports use the shared tax/filing readers; retained reports preserve their original values and filing observations after reopening or later filing.

Charged, collected, refunded, remitted, liability, recoverable, cash-recovered, offset, and nonrecoverable cents stay separate. A filed zero remains `0`; unknown filing dates/amounts/due dates stay blank. A missing older filing snapshot is marked `Not retained` and produces no invented filing rows. Unsupported filing snapshot versions stop projection. Private reasons/evidence and raw snapshots stay out; actual reference numbers remain readable.

Three table cases and two tax kernel cases pass in Release. They cover actual partial recovery and filed zero with exact remaining tax despite later recovery, retained versus current filing after reopening, and a controlled legacy snapshot without filing detail. The latest complete suite is 909 before supplier/tax additions; another full run and Windows Debug build are in progress. Other report groups and verified Sheets publication remain pending.
