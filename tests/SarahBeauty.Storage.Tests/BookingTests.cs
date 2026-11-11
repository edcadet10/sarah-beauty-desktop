using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;

namespace SarahBeauty.Storage.Tests;

public sealed class BookingTests
{
    [Fact]
    public void NamingPlacesDoesNotChangeTheCountOrPrice()
    {
        using var fixture = new BusinessFixture();
        var book = new BookingBook(fixture.Store);
        var customer = new CustomerBook(fixture.Store).Save(fixture.Owner, new Customer { FullName = "Example guest" });
        var booking = book.Save(fixture.Owner, BookingRules.Wedding("Example wedding", 6));
        var guestCharge = booking.Charges.Single(c => c.Unit == "Person");
        foreach (var place in booking.Places.Where(p => p.ChargeId == guestCharge.Id).Take(2))
            booking = book.LinkPlace(fixture.Owner, booking, place.Id, customer.Id);
        Assert.Equal(2, booking.Places.Count(p => p.ChargeId == guestCharge.Id && p.CustomerId.Length > 0));
        Assert.Equal(4, booking.Places.Count(p => p.ChargeId == guestCharge.Id && p.CustomerId.Length == 0));
        Assert.Equal(102000, BookingRules.Totals(booking).NetCents);
        Assert.Single(new CustomerBook(fixture.Store).Search(fixture.Owner));
        Assert.Equal(booking.Id, Assert.Single(book.List(fixture.Owner)).Id);
    }

    [Fact]
    public void QuantityChangesPreserveNamedPlacesAndHistoryOfRemovedUnnamedPlaces()
    {
        var booking = BookingRules.Wedding("Example", 6);
        string chargeId = booking.Charges.Single(c => c.Unit == "Person").Id;
        booking = BookingRules.ChangeQuantity(booking, chargeId, 3);
        Assert.Equal(3, booking.Places.Count(p => p.ChargeId == chargeId && p.Active));
        Assert.Equal(3, booking.Places.Count(p => p.ChargeId == chargeId && !p.Active));
        booking = BookingRules.ChangeQuantity(booking, chargeId, 5);
        Assert.Equal(5, booking.Places.Count(p => p.ChargeId == chargeId && p.Active));
    }

    [Fact]
    public void TrialPlanRequiresExplicitTaxAndCustomPlanDoesNotAssumeATrial()
    {
        var booking = BookingRules.Wedding("Example", 9, 76.6m);
        Assert.Null(BookingRules.Totals(booking).TotalCents);
        booking = booking with { Charges = booking.Charges.Select(c => c with { TaxRate = 0m }).ToArray() };
        var totals = BookingRules.Totals(booking);
        Assert.Equal(145660, totals.TotalCents);
        Assert.Equal(65330, totals.Plan!.DepositCents);
        var other = booking with { HasTrial = false, CustomDepositCents = 10000 };
        Assert.Equal(0, BookingRules.Totals(other).Plan!.TrialCents);
    }

    [Fact]
    public void SameArtistOverlapRequiresAnOverrideWhileAnotherArtistCanWork()
    {
        using var fixture = new BusinessFixture();
        var book = new BookingBook(fixture.Store);
        var firstArtist = book.SaveArtist(fixture.Owner, new Artist { Name = "Artist one" });
        var secondArtist = book.SaveArtist(fixture.Owner, new Artist { Name = "Artist two" });
        var appointment = new Appointment(Guid.NewGuid().ToString(), "Wedding", firstArtist.Id,
            DateTimeOffset.Parse("2027-01-02T10:00:00-05:00"), DateTimeOffset.Parse("2027-01-02T11:00:00-05:00"), "Example venue");
        book.Save(fixture.Owner, BookingRules.Wedding("First") with { Appointments = [appointment] });
        var second = BookingRules.Wedding("Second") with { Appointments = [appointment with { Id = Guid.NewGuid().ToString() }] };
        Assert.Throws<ArgumentException>(() => book.Save(fixture.Owner, second));
        book.Save(fixture.Owner, second with { Appointments = [second.Appointments[0] with { ArtistId = secondArtist.Id }] });
    }

    [Fact]
    public void DirectConfirmationAndCrossBusinessCustomerLinksAreRejected()
    {
        using var fixture = new BusinessFixture();
        var book = new BookingBook(fixture.Store);
        Assert.Throws<ArgumentException>(() => book.Save(fixture.Owner, BookingRules.Wedding("Example") with { Status = "Confirmed" }));
        Assert.Throws<ArgumentException>(() => book.Save(fixture.Owner, BookingRules.Wedding("Example") with { PrimaryCustomerId = Guid.NewGuid().ToString() }));
        Assert.Empty(book.List(fixture.Owner));
    }
}
