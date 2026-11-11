# Plan appointments

Appointments link an artist and actual recipient places to their schedule. Saving an appointment does not create a service charge, customer record, or payment.

1. Open a saved booking and expand **Appointments**. Add the artist if needed. Creating an artist keeps the edited booking details in the editor.
2. Expand **New appointment** and choose Trial, Wedding, or Other. Select the actual artist.
3. Choose each recipient place and select **Add appointment recipient**. An unnamed place stays unnamed until you link its actual customer under Party members. Leave undecided recipients unassigned rather than inventing names.
4. Enter the appointment time zone, location, and start with its actual UTC offset. For example, `2027-01-10T10:00-05:00` identifies both a local clock time and its offset. A blank zone remains unconfirmed.
5. Enter the end, or select **Calculate end from known service durations**. The calculation uses durations copied into the booking. Unknown durations need review or an explicitly entered end. A calculated end is a proposal; it does not save the appointment or confirm that work occurred.
6. Review any artist or recipient conflict. Change the times or record the explicit scheduling override before saving.
7. Select the actual status, add private products/shades or notes if needed, and select **Save appointment**.

The booking retains its recipient places and service quantities. Scheduling a recipient with two artists does not increase the booked quantity. Cancelling a booking releases its scheduled time while retaining the original appointments.

## Review event readiness and clock changes

Enter the event's ready-by local time and time zone in the booking details. If that local time occurs twice during a clock change, enter its actual UTC offset too. A nonexistent local time is rejected. Reservation checks use this deadline and the accepted quote's event zone.

Appointment start and end offsets must match their stated zone for those local times. Calculating an elapsed duration across a clock change can produce different offsets at the two ends. The editor retains both; do not replace them with a fixed seasonal offset.

Changing the quoted event time zone requires a revised accepted agreement before reservation. Already-issued documents retain the zone and offset they captured.
