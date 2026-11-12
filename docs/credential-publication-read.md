# Check credential absence under the publication lock

The full native-creation regression at `f8b62f8` passed 1,513 cases and failed one concurrent credential replacement case: a read returned no protected value. A separate single-case rerun passed, so that rerun did not establish that the race was resolved.

The reader checked file existence before acquiring the shared publication lock. A controlled test held the writer's lock, temporarily parked the prior file, and started a reader. Before correction the reader returned absent rather than waiting. The reader now checks whether the scope directory exists, takes the publication lock, revalidates paths/current store context and only then decides whether the credential file is absent. Missing scope directories still create no credential storage.

All 29 credential-file/connection/token-refresh cases pass, including the failing-before publication probe. Ten additional rounds of twelve concurrent writer/read pairs pass with whole protected values. Windows Debug passes with zero warnings/errors. No actual credentials were used or logged; the protected fixtures contain fictional test bytes.

The full corrected local regression at `026f142` passes all 1,515 cases in 6 minutes 54 seconds. Hosted verification is running. The fix changes no database schema, credential context, encryption format or stored payload. Prior native-recovery source `6f50c77` passed 1,514 local cases; hosted workspace-selection source `eaf43ea` passed 1,514 cases, Windows Debug and recovery cryptography. Those earlier passes do not replace verification of this concurrency fix.
