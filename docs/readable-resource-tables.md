# Project suppliers, assets, and loan registers

Status: Explicit supplier, loan-register, asset-register, and registered-asset-review tables are implemented internally as another partial readable group. Operational/purchasing/banking/tax groups, reports, transport/publication, and UI follow separately.

Supplier rows retain business contact/terms and provenance while omitting private notes. Loans retain actual lender/opening/terms/due/owner-lender facts. Unknown opening dates/principal remain blank; a recorded zero stays `0`. Asset rows retain legal owner, acquisition/service facts, registered cost, tax basis, business-use fraction, funding/serial, disposal reference, and provenance. Unreviewed values stay blank rather than becoming zero.

Asset fact history retains request identity, original order, previous/new registered values, actor, and timestamp. Free-form review reasons/evidence inputs remain in the desktop/archive. Registered cost and tax basis are not automatically book carrying value; the export does not post or recalculate depreciation/disposal.

Two focused Debug cases pass: supplier privacy, unknown versus zero loan/asset facts, and an actual reviewed asset-fact change with previous unknown values retained and no journal movement. The desktop Debug build has no warnings/errors. The latest complete Release suite has 872 passing cases before these additions. No Google request or complete-export claim occurred.
