# Keep TEST and LIVE recovery selections separate

Recovery paths now carry an explicit mode. TEST keeps its existing primary database, selection and lock filenames. LIVE resolves its clean `Primary` database and separate `active-live-store.json` selection and lock. Path resolution creates no data.

Selections retain their version-1 format and add an explicit mode. Existing TEST selections without that optional field remain TEST-only and are read without rewriting. Mode mismatches, noncanonical modes and empty request/store/instance identities are rejected. The shared record is now named `ActiveStoreSelection`; persisted identities and paths do not change.

Two probes initially failed: reading a missing selection created a lock in an existing root and threw for a missing root. A read now returns absent before opening the publication gate when no selection file exists. Existing-file reads still use the gate, reread the file and validate the complete selection.

Thirty-four mode-path/TEST-activation cases pass, including the two corrected probes, mode-separated round trips, legacy compatibility, invalid identities, activation rollback/retry and concurrent pointer readers. Windows Debug passes with zero warnings/errors. The parent full creation/opening regression passes 1,477 cases at `fd4adc7`; hosted verification is running. LIVE activation and native selection follow separately.
