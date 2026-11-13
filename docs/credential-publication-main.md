# Wait for credential publication before reading absence

This focused change brings the credential-reader fix into `main` before the LIVE creation interface. The verified Windows preview already contains the same correction through the later feature stack. It changes no database schema, protected format, or credential payload.

The reader previously tested file existence before taking the publication lock. A writer replacing the file could make the reader report an absent value. The reader now preserves the no-directory fast path, takes the lock, rechecks the path and current store, and then tests file existence before opening it.

The falsifying test holds the actual writer lock, temporarily parks an existing fictional protected file, and starts a reader. Reporting absence during that locked interval rejects the claim that the reader waits for publication. At main source `860e99c`, the new test fails with that exact outcome. After the two-line correction, all 29 credential-file, connection, and token-refresh Release cases pass, including the controlled replacement case. No actual credentials or external Google requests are used.

The tests retain the original missing-directory, protected-value, file-bound, and context checks. Complete hosted storage/build and recovery-cryptography checks run on the focused branch before integration.
