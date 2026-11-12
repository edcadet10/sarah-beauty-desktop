# Read captured dashboard financial datasets

The dashboard finance reader now has an internal database/business/date path using the complete captured report, existing monthly coverage, aging, expense categories, cash balances, and verified reconciliation queries. Existing human calls retain their read hook and check access before returning. Captured calls do not create or renew a human session.

Monthly figures remain unknown when neither activity nor completed close coverage establishes them. Financing cash remains separate from income, aging retains its original milestone sources, and cash accounts retain original reconciliation identity/hash/date. One timestamp and watermark flow through the captured report and chart sources. Raw dashboard/report objects remain internal; only selected table projections can become readable payloads.

Two new cases plus eighteen related dashboard/reconciliation cases pass in Debug (20 targeted). They compare the complete actual dashboard against the human result after later month activity/lock, preserve an unknown month, retain exact aging, reject late public lock, and enforce the existing 120-month/date limits. The preceding complete Release suite passes all 943 cases in 2 minutes 36 seconds. Windows Debug has zero warnings/errors. Chart projection and upcoming datasets follow separately.
