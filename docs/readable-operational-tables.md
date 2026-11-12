# Project supplies and actual mileage history

Status: Explicit supply/status-history, actual-trip/review-history, and trip-payment attribution/release tables are implemented internally as another partial readable group. Purchasing/banking/tax groups, reports, transport/publication, and UI follow separately.

Supply rows retain product/brand/shade, status, location, purchase reference, known/unknown opening/expiry dates, archive state, and provenance. Status history retains earlier dates/decisions. Private notes remain in the desktop/archive. These records do not imply retail sales, create expenses, or automatically dispose of a supply.

Trip rows retain actual route/distance, driver/booking identity, parking/tolls, reimbursement review state, nullable requested amount, archive state, and provenance. Review history preserves earlier facts. Actual miles remain separate from quoted/billed customer travel quantity. Unknown requested amounts stay blank; a confirmed zero stays `0`. Evidence input text is omitted.

Payment-attribution and release rows retain existing payment/link identities, dates/cents, approval/request/hash facts, and the frozen claim-basis hash/trip revision. Private payee/evidence/release explanations and raw snapshots stay out. These are attributions of already posted payments, not additional expenses/cash.

Three new cases and two register cases pass in Release (five targeted cases): supply dates/history/privacy; actual versus billed mileage and unknown/zero claim history; and actual payment attribution/release with frozen basis and one cash expense. The latest complete suite has 872 passing cases before register/operational additions. No Google request or complete-export claim occurred.
