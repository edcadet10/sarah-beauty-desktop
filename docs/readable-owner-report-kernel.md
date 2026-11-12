# Read captured owner schedules

The existing owner schedule now accepts an internal business ID alongside the debt schedule from the same captured source. Existing human calls delegate to the same calculation after their owner check. No session is fabricated or renewed, no percentage-based allocation is introduced, and no money is posted.

Contributions, draws, owner payable changes, reimbursements, and captured owner-lender movements remain separate. Missing historical lender attribution stays unconfirmed even if the current register links an owner. The caller must supply debt facts from the same authorized database/business/date scope.

Two new cases and ten existing owner/debt schedule cases pass in Debug (12 targeted). They compare actual two-owner movements and loan attribution with the human report, preserve captured results after a later contribution and owner lock, retain legacy attribution absence, and return no foreign owner/loan rows. The complete Release suite before these additions passes all 926 cases in 2 minutes 27 seconds. Windows Debug has zero warnings/errors. Owner table projection follows separately.
