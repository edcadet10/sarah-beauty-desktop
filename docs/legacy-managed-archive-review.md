# Review an older archive with managed evidence

On November 13, 2026, the downloaded Windows preview at `6cd916a4127ce17a2032e101bbcf78bf694f7c9c` reviews an existing fictional schema-31 encrypted archive before business setup or owner sign-in. This checks retained managed evidence as well as generated documents; the other recent offline archive fixture contains generated PDFs.

## Validate without activating a business

The app starts in a fresh disposable TEST directory. Its native archive and protected-material pickers select the existing fictional files. An incorrect recovery passphrase rejects. The correct private test passphrase completes validation and reports TEST/schema 31, two movements/five journal lines, and three managed file versions. With no local business supplied, the result explicitly says that no local backup record was supplied.

The file manifest includes the database and the retained managed files. Closing the staged review removes its one known temporary plaintext workspace. Returning to owner setup shows the business-creation form; no recovered copy is activated. The original encrypted archive and protected material retain their exact hashes. The encrypted archive SHA-256 is `FED4A40827D0C6098D501C529C5EE2CE8107AC14FBBEFB2C4AFDDFB8F0156A3A`.

The claim is limited to this older archive's validation and cleanup. A wrong-passphrase success, missing managed version, changed retained journal counts, altered original file, or retained stage would reject it; none occurs. This probe runs on the development computer and does not establish second-computer writable activation, physical Surface acceptance, or a network-blocked result for this particular archive. The separate [blocked-network archive review](windows-preview.md) remains documented with its own fixture.
