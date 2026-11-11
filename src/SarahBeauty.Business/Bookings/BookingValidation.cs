using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

internal static class BookingValidation
{
    internal static void Validate(DbWork db, OwnerSession owner, Booking booking, IReadOnlyList<Booking> others, IReadOnlyList<Artist> artists)
    {
        if (string.IsNullOrWhiteSpace(booking.Name) || booking.Name.Length > 200) throw new ArgumentException("Enter a booking name of up to 200 characters.");
        if (!new[] { "Undecided", "Single", "Split" }.Contains(booking.PayerMode)) throw new ArgumentException("Choose a payer arrangement.");
        Customer(db, owner, booking.PrimaryCustomerId);
        foreach (var charge in booking.Charges.Where(c => c.Active)) ValidateCharge(db, owner, booking, charge);
        foreach (var place in booking.Places) Customer(db, owner, place.CustomerId);
        if (booking.Charges.Select(c => c.Id).Distinct().Count() != booking.Charges.Length
            || booking.Places.Select(p => p.Id).Distinct().Count() != booking.Places.Length)
            throw new ArgumentException("Each charge and place needs a distinct identity.");
        if (booking.Places.Any(p => !booking.Charges.Any(c => c.Id == p.ChargeId)))
            throw new ArgumentException("Every place must belong to a booking charge.");
        if (booking.Places.Any(p => p.Active && !booking.Charges.Any(c => c.Id == p.ChargeId && c.Active && c.PersonBased)))
            throw new ArgumentException("Active places require an active person-based charge.");
        AppointmentRules.Validate(booking, others, artists);
        _ = BookingRules.Totals(booking);
    }

    private static void ValidateCharge(DbWork db, OwnerSession owner, Booking booking, BookingCharge charge)
    {
        if (string.IsNullOrWhiteSpace(charge.Description) || !new[] { "Person", "Package", "Mile", "Custom" }.Contains(charge.Unit)
            || charge.Quantity <= 0 || charge.Quantity > 100000 || charge.UnitCents < 0 || charge.DiscountCents < 0
            || charge.NetCents < 0 || charge.TaxRate is < 0 or > 1)
            throw new ArgumentException("Check each charge's description, unit, quantity, amount, discount, and tax rate.");
        if (charge.PersonBased && (charge.Quantity != decimal.Truncate(charge.Quantity)
            || booking.Places.Count(p => p.ChargeId == charge.Id && p.Active) != charge.Quantity))
            throw new ArgumentException("Named and unnamed places must equal the booked quantity.");
        Customer(db, owner, charge.PayerCustomerId);
    }

    internal static void Customer(DbWork db, OwnerSession owner, string id)
    {
        if (id.Length > 0 && CustomerBook.Find(db, owner, id) is null)
            throw new ArgumentException("A linked customer does not belong to this business.");
    }
}
