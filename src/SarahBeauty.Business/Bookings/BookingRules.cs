using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Bookings;

public static class BookingRules
{
    public static Booking Wedding(string name, int guests = 0, decimal miles = 0)
    {
        var booking = new Booking { Id = Guid.NewGuid().ToString(), Name = name };
        booking = AddCharge(booking, "Bridal makeup including trial", "Package", 1, 30000);
        if (guests > 0) booking = AddCharge(booking, "Non-bride makeup", "Person", guests, 12000);
        if (miles > 0) booking = AddCharge(booking, "Wedding travel (round-trip miles)", "Mile", miles, 100);
        return booking;
    }

    public static Booking AddCharge(Booking booking, string description, string unit, decimal quantity, long unitCents)
    {
        var charge = new BookingCharge(Guid.NewGuid().ToString(), description.Trim(), unit, quantity, unitCents);
        var places = charge.PersonBased ? NewPlaces(charge, 1, quantity) : [];
        return booking with { Charges = [.. booking.Charges, charge], Places = [.. booking.Places, .. places] };
    }

    public static Booking ChangeQuantity(Booking booking, string chargeId, decimal quantity)
    {
        var charge = booking.Charges.Single(c => c.Id == chargeId);
        if (quantity < 0 || quantity > 100000 || (charge.PersonBased && (quantity > 1000 || quantity != decimal.Truncate(quantity))))
            throw new ArgumentException("Use a nonnegative whole number for people.");
        var places = booking.Places.ToList();
        var active = places.Where(p => p.ChargeId == chargeId && p.Active).ToList();
        if (charge.PersonBased && quantity < active.Count(p => p.CustomerId.Length > 0))
            throw new ArgumentException("Unlink named recipients before reducing this quantity.");
        foreach (var remove in active.Where(p => p.CustomerId.Length == 0).Reverse().Take(Math.Max(0, active.Count - (int)quantity)))
            places[places.IndexOf(remove)] = remove with { Active = false };
        int next = places.Where(p => p.ChargeId == chargeId).Select(p => p.Ordinal).DefaultIfEmpty(0).Max() + 1;
        if (charge.PersonBased && quantity > active.Count) places.AddRange(NewPlaces(charge, next, quantity - active.Count));
        return booking with { Charges = booking.Charges.Select(c => c.Id == chargeId ? c with { Quantity = quantity, Active = quantity > 0 } : c).ToArray(), Places = places.ToArray() };
    }

    public static BookingTotals Totals(Booking booking)
    {
        var charges = booking.Charges.Where(c => c.Active).ToArray();
        long net = charges.Aggregate(0L, (sum, c) => checked(sum + c.NetCents));
        long? tax = charges.All(c => c.TaxRate.HasValue)
            ? charges.Aggregate(0L, (sum, c) => checked(sum + Money.Multiply(c.TaxRate!.Value, c.NetCents))) : null;
        long? total = tax.HasValue ? checked(net + tax.Value) : null;
        PaymentPlan? plan = total is null ? null : booking.HasTrial ? PaymentPlan.Bridal(total.Value, booking.TrialCents)
            : booking.CustomDepositCents.HasValue ? PaymentPlan.Custom(total.Value, booking.CustomDepositCents.Value) : null;
        return new(net, tax, total, plan);
    }

    private static BookingPlace[] NewPlaces(BookingCharge charge, int firstOrdinal, decimal count)
    {
        if (count < 0 || count > 1000 || count != decimal.Truncate(count)) throw new ArgumentException("Use 0 to 1,000 whole people.");
        return Enumerable.Range(firstOrdinal, (int)count).Select(n => new BookingPlace(Guid.NewGuid().ToString(), charge.Id, n)).ToArray();
    }
}
