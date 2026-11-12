# Project service and booking records

Status: Explicit catalog and booking tables are implemented internally as another partial readable group. Appointment/status/reservation tables, documents, financial/resource groups, reports, transport, and verified workbook publication follow separately.

The group includes current service catalog rows, retained service revisions, bookings, individual charges, and named/unnamed places. Parent rows retain IDs, revisions, source generation/watermark, and creation/update provenance. Child rows retain explicit parent IDs, active/history flags, original service revisions, quantities, copied prices, and nullable durations/tax. Inactive places remain visible. Unnamed customer IDs remain blank.

Booking totals and trial/deposit/final amounts use the existing booking rules. Unresolved tax/total/plan values stay blank. Confirmed zero tax stays `0`. Undecided event/due dates remain blank. Updated catalog prices do not rewrite booking charges or older service revisions. Fractional tax rates and exact quantities use invariant decimal text; money columns contain exact cents text.

Booking private notes and free-form service tax-review evidence are omitted. Tax review status remains visible; supporting evidence stays in the desktop/archive. This group sends no generic record JSON, media, or authentication data. Readable record loading checks relational versus retained JSON identity/revision before projecting.

Three new cases pass: retained versus current prices, exact fractional travel and amounts, named/unnamed/inactive place retention, private-field exclusion, unknown tax versus confirmed zero, existing plan calculations with undecided dates, and rejection of mismatched retained record identity. The latest full suite has 853 passing cases before these additions. No Google request or complete-export claim occurred.
