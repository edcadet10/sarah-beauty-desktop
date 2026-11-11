using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class OperationsTests
{
    [Fact]
    public void SupplyStatusAndExpiryAlertsRetainHistoryWithoutValuingConsumption()
    {
        using var f=new LedgerFixture();
        var book=new OperationalResources(f.Business.Store);
        var supply=book.SaveSupply(f.Business.Owner,new Supply{Name="Fictional foundation",Brand="Fictional brand",Shade="Example",
            OpenedDate=f.Date,ExpiryDate=f.Date.AddDays(30)});
        Assert.Empty(book.Alerts(f.Business.Owner,f.Date));
        supply=book.SaveSupply(f.Business.Owner,supply with{SupplyStatus="Low"});
        Assert.Single(book.Alerts(f.Business.Owner,f.Date));
        supply=book.SaveSupply(f.Business.Owner,supply with{SupplyStatus="Finished"});
        Assert.Equal(3,supply.History.Length);
        Assert.Throws<ArgumentException>(()=>book.SaveSupply(f.Business.Owner,supply with{History=[]}));
        book.SaveSupply(f.Business.Owner,supply with{Archived=true});
        Assert.Empty(book.Alerts(f.Business.Owner,f.Date.AddDays(60)));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ExpiryIsInclusiveAndInvalidDatesOrStaleSupplyEditsAreRejected()
    {
        using var f=new LedgerFixture();
        var book=new OperationalResources(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>book.SaveSupply(f.Business.Owner,new Supply{Name="Fictional invalid dates",OpenedDate=f.Date,ExpiryDate=f.Date.AddDays(-1)}));
        var supply=book.SaveSupply(f.Business.Owner,new Supply{Name="Fictional expiring product",ExpiryDate=f.Date});
        Assert.Single(book.Alerts(f.Business.Owner,f.Date));
        book.SaveSupply(f.Business.Owner,supply with{Location="Fictional storage drawer"});
        Assert.Throws<InvalidOperationException>(()=>book.SaveSupply(f.Business.Owner,supply with{Location="Fictional stale edit"}));
    }

    [Fact]
    public void ActualTripsRequireOneRealDriverAndDoNotChangeQuotedTravelOrPostMoney()
    {
        using var f=new LedgerFixture();
        var bookings=new BookingBook(f.Business.Store);
        var booking=QuoteTests.Booking(f.Business);
        var book=new OperationalResources(f.Business.Store);
        var trip=new MileageTrip{Name="Fictional venue visit",TripDate=f.Date,BookingId=booking.Id,
            Origin="Fictional studio",Destination="Fictional venue",ActualMiles=42.7m,ParkingCents=500,TollsCents=125};
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip));
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{DriverOwnerId=Guid.NewGuid().ToString()}));
        var artist=bookings.SaveArtist(f.Business.Owner,new Artist{Name="Fictional trip artist"});
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{DriverOwnerId=f.Business.Owner.OwnerId,DriverArtistId=artist.Id}));
        var saved=book.SaveTrip(f.Business.Owner,trip with{DriverArtistId=artist.Id,ReimbursementStatus="Requested"});
        Assert.Equal(42.7m,saved.ActualMiles);
        var unchanged=bookings.List(f.Business.Owner).Single();
        Assert.Equal(booking.Revision,unchanged.Revision);
        Assert.Equal(booking.Charges,unchanged.Charges);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
}
