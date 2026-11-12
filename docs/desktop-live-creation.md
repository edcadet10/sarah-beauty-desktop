# Review and create a clean LIVE business

The empty LIVE screen now offers a separate creation form. It reuses the private profile fields with unknown defaults, adds two owner names and repeated word-passphrase entries, and requires a private creation reason. The shared core preflight validates the same creation inputs before review and again before filesystem writes.

The final dialog freezes the reviewed profile and owner names, masks supplied EIN detail, lists pending operational reviews, and states that ownership/openings remain unconfirmed. It does not display passphrases. Keeping the draft creates no data and clears the secret fields. Final confirmation runs the staged creation service under application shutdown tracking. Navigation cancels the operation and locks a late returned owner session rather than reopening an old screen.

Successful creation enters the LIVE workspace through the same verified owner/store handoff as recovered activation. All background runtimes use that opened store. A shared profile-summary component keeps creation and later profile confirmation consistent.

Forty-one creation/opening/policy cases pass. Windows Debug passes with zero warnings/errors. Native checks reject missing settings/scope and mismatched repeats, keep editing without creating a LIVE folder, then explicitly create the business, load profile revision 1, sign in as both owners and reopen after restart. The existing fictional TEST database remains byte-for-byte unchanged.

Independent checks verify schema 39, LIVE mode, watermark 3, two new owners with separate salts, one profile and three audits. Currency/UTC/start/scope are explicit; legal/EIN/reporting-year and setup reviews remain unknown. Ownership and account openings are unconfirmed; no TEST customers, files, documents or financial entries are copied. Pending creation stages are absent after success. Native exact TEST/LIVE recovered-activation retries and their retained-data checks also pass with this build.

The full local native-recovery parent regression passes 1,514 cases. A full creation-screen regression follows. Actual business setup values, complete release walkthrough, real Google authorization/delivery and Surface/second-device acceptance remain pending; these checks use isolated fictional records.
