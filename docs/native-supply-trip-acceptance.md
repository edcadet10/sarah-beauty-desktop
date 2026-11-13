# Review packaged supply alerts and actual trips

Date: November 12, 2026. Application: self-contained Windows x64 preview from `9b667a8`, schema 40. This extends the [fictional supplier fixture](native-supplier-acceptance.md). Product dates, travel, and costs are TEST inputs, not actual business evidence.

## AC-034 native checks

The owner creates one supply with opened date November 1, expiry November 11, and status Available. Separate native saves change it to Low and then Finished. The retained history contains exactly Available → Low → Finished with the same dates, owner identities, and times. Status is not treated as a consumption quantity or inventory valuation.

For planning date November 12, the refreshed native dashboard shows Low and expiry alerts after the Low save, then Finished and expiry alerts after the Finished save. Counts change from Low 1/Finished 0 to Low 0/Finished 1, while expiry remains 1. The dashboard is a captured view: the harness initially read an earlier snapshot/offscreen summary; explicit Refresh and scrolling make the current result available. Those interrupted attempts are not counted as passes.

The owner records an actual November 11 trip with one owner driver, fictional store-to-studio route, 20 actual miles, $5 parking, $2 tolls, and Reimbursement review = Not requested. Requested reimbursement remains blank/unknown. The saved trip retains one original review-history entry. No quoted customer travel rate, tax mileage rate, reimbursement, or expense is inferred.

## Independent checks and restart

Read-only checks verify all supply dates/status history and exact trip/driver/route/cost fields. Every earlier customer, register, draft, movement, journal, account, document, and supplier-settlement row remains unchanged. All fourteen existing movements and 32 journal lines remain, with zero new money from status, expiry, mileage, parking, or toll logging. Integrity and foreign keys pass.

The second owner restarts the packaged app, refreshes and sees Finished/expiry alerts, and opens the exact trip fields with reimbursement still unapproved. Complete final stored rows match the checkpoint after restart.

This covers AC-034's stated native fixture. Physical travel/evidence, actual reimbursements and tax policy, devices, real Google delivery, signing, and other full release acceptance remain separate work. No production code changed for this check.
