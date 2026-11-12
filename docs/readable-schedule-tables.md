# Project appointments and reservation history

Status: Artist, appointment, recipient-link, booking-status, and reservation tables are implemented internally as another partial readable group. Other domain groups, reports, transport, verified publication, and desktop export remain pending.

Artist rows retain identity/revision, archive state, and creation/update provenance. Appointments retain their booking status/archive state as well as their own status, so a planned appointment in a cancelled booking is not presented without its parent context. They include kind, artist, location, UTC instants, originally recorded timestamps/offsets, and the saved time-zone ID. Unknown times stay blank. Recipient-place links use explicit IDs.

Booking status history retains source order, states, actor, and date. Reservation rows retain request identity, exact accepted quote version, date, trial/deposit cents, actor, and timestamp. These are copied recorded decisions; the export creates no new reservation or payment. Private artist/booking/appointment notes, products/shades, override explanations, and free-form status/reservation reasons remain in the desktop/archive.

Three cases pass: a real ambiguous-hour appointment with distinct saved offsets and cancelled-parent context; an actually approved/applied deposit and confirmed reservation with exact quote/amounts; and unknown appointment times plus retained archived artists. Private sentinels stay out. No Google request or complete workbook claim occurred. The latest complete suite has 853 passing cases before the planning and schedule additions.
