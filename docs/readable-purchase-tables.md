# Project supplier bills, credits, and settlements

Status: Explicit supplier-bill/line, supplier-credit/reduction, and settlement tables are implemented internally as another partial readable group. Banking/tax/evidence groups, reports, transport/publication, and UI follow separately.

Bills retain supplier/reference/dates/status/approval/provenance and exact line quantities, cost, paid tax, business-use fraction, computed gross/business portions, and account/asset/booking links. Missing tax/use keeps dependent amounts blank. Bill approval is not another cash expense.

Supplier-credit rows retain original purchase identity, dates/references/approval, linked reversal identity, sign, and exact credited line amounts/tax/business portions. Free-form credit reasons, evidence inputs, and cached generations stay in the desktop/archive. Positive rows with negative reversal signs must retain that meaning.

Settlements retain original posted movement, bill/line, dates, total/business/personal-draw portions, sign, reversal/refund/consumption links. These are allocations of existing movements, not additional cash or expense. Registered asset values and current supplier balances remain separate report concepts.

Three cases pass: actual incomplete/approved bills with unknown versus known line facts and no posting; actual supplier credit/reversal with signed reductions/private-reason exclusion; and an actual mixed business-cash payment with separate business/draw portions and one cash movement. The latest complete suite has 872 passing cases before register/operational/purchase additions. No Google request or complete-export claim occurred.
