# Project retained financial details

Four source-bound tables retain frozen loan/owner-lender identities, chosen asset-refund treatment, selected supplier-refund line amounts, and explicit liability/recoverable/nonrecoverable tax-refund allocations. `SourceKind` separates current draft facts from immutable approved movement facts; do not sum both as cash. An absent tax allocation has no row, while a chosen zero component remains `0`.

Approved detail and the existing movement projection share one internal reader that checks retained draft identity/revision/kind/date/gross/tax against relational posting facts. Source hashes are verified when capturing the private snapshot. Current loan-register names cannot rewrite historical lender names. Cached approval generations and private evidence inputs stay out.

Four new cases and three existing ledger projection cases pass in Release for lender rename/reversal, exact mixed tax treatment, a selected supplier-refund line, and actual asset recovery treatment with no additional postings. PR #151's validated reader-scheduling fix is merged from main into this branch. A complete Release run and Windows Debug build are in progress; their result is recorded separately when finished. This is a partial projection; Sheets publication remains pending.
