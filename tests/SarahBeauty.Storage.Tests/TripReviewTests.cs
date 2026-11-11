using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class TripReviewTests
{
    internal static MileageTrip Trip(LedgerFixture f)=>new(){Name="Fictional actual business trip",TripDate=f.Date,DriverOwnerId=f.Business.Owner.OwnerId,
        Origin="Fictional origin",Destination="Fictional destination",ActualMiles=18.4m,ParkingCents=250,TollsCents=100};

    [Fact]
    public void UnknownRequestedAmountIsNotZeroAndNoReviewCreatesAnExpenseOrPayment()
    {
        using var f=new LedgerFixture();var book=new OperationalResources(f.Business.Store);var trip=book.SaveTrip(f.Business.Owner,Trip(f) with{ReimbursementStatus="Requested"});
        Assert.Null(trip.RequestedReimbursementCents);Assert.Null(trip.History.Single().RequestedReimbursementCents);
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{RequestedReimbursementCents=0}));
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{RequestedReimbursementCents=-1}));
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{ReimbursementStatus="Not requested",RequestedReimbursementCents=100}));
        var zero=book.SaveTrip(f.Business.Owner,trip with{ReimbursementStatus="Not requested",RequestedReimbursementCents=0});Assert.Equal(0,zero.RequestedReimbursementCents);
        Assert.Empty(f.Book.Posted(f.Business.Owner));Assert.Equal(0,f.Balance("5010"));Assert.Equal(0,f.Balance("1010"));
    }

    [Fact]
    public void EveryReviewRetainsItsActualDetailsAndDoesNotChangeCustomerTravelCharges()
    {
        using var f=new LedgerFixture();var bookings=new BookingBook(f.Business.Store);var wedding=bookings.Save(f.Business.Owner,BookingRules.Wedding("Fictional independently quoted travel",2,76.6m));
        var book=new OperationalResources(f.Business.Store);var first=book.SaveTrip(f.Business.Owner,Trip(f) with{BookingId=wedding.Id,Origin=" Fictional origin ",Destination=" Fictional destination "});
        var second=book.SaveTrip(f.Business.Owner,first with{ActualMiles=24.8m,RequestedReimbursementCents=2150,ReimbursementStatus="Requested",EvidenceId="Fictional reviewed claim agreement"});
        var third=book.SaveTrip(f.Business.Owner,second with{RequestedReimbursementCents=0,ReimbursementStatus="Not requested",Archived=true});
        var loaded=new OperationalResources(f.Business.Store).Trips(f.Business.Owner).Single();
        Assert.Equal(3,loaded.Revision);Assert.Equal(new long?[]{null,2150,0},loaded.History.Select(h=>h.RequestedReimbursementCents));
        Assert.Equal(18.4m,loaded.History[0].ActualMiles);Assert.Equal(24.8m,loaded.History[1].ActualMiles);Assert.True(loaded.History[2].Archived);
        Assert.Equal("Fictional origin",loaded.History[0].Origin);Assert.Equal("Fictional destination",loaded.History[0].Destination);
        Assert.All(loaded.History,h=>{Assert.Equal(f.Business.Owner.OwnerId,h.OwnerId);Assert.NotEmpty(h.AtUtc);});
        Assert.Equal(BookingRules.Totals(wedding).NetCents,BookingRules.Totals(bookings.List(f.Business.Owner).Single()).NetCents);
        Assert.Equal(third.Id,loaded.Id);Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void HistoryCannotBeReplacedInventedOrWrittenThroughAnotherBusinessSession()
    {
        using var f=new LedgerFixture();var book=new OperationalResources(f.Business.Store);var trip=book.SaveTrip(f.Business.Owner,Trip(f));
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{History=[trip.History[0] with{ActualMiles=999}]}));
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,Trip(f) with{History=trip.History}));
        book.SaveTrip(f.Business.Owner,trip with{EvidenceId="Fictional later evidence"});
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,trip with{EvidenceId="Fictional stale trip review"}));
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.SaveTrip(foreign.Owner,Trip(f)));
    }

    [Fact]
    public void HistoricalArtistCanRemainAfterArchiveButCannotBeAssignedNewTrips()
    {
        using var f=new LedgerFixture();var artists=new BookingBook(f.Business.Store);var artist=artists.SaveArtist(f.Business.Owner,new(){Name="Fictional historical driver"});
        var book=new OperationalResources(f.Business.Store);var trip=book.SaveTrip(f.Business.Owner,Trip(f) with{DriverOwnerId="",DriverArtistId=artist.Id});
        artists.SaveArtist(f.Business.Owner,artist with{Archived=true});
        var saved=book.SaveTrip(f.Business.Owner,trip with{EvidenceId="Fictional historical receipt evidence"});
        Assert.Equal(artist.Id,saved.DriverArtistId);Assert.Equal(2,saved.History.Length);
        Assert.Throws<ArgumentException>(()=>book.SaveTrip(f.Business.Owner,Trip(f) with{DriverOwnerId="",DriverArtistId=artist.Id}));
    }

    [Fact]
    public void OlderTripJsonKeepsUnknownAmountsAndBeginsHistoryWithoutInventingPastReviews()
    {
        using var f=new LedgerFixture();var book=new OperationalResources(f.Business.Store);var trip=book.SaveTrip(f.Business.Owner,Trip(f));
        f.Business.Files.Execute($"UPDATE Records SET DataJson=json_remove(DataJson,'$.History','$.RequestedReimbursementCents') WHERE Id='{trip.Id}';");
        var legacy=book.Trips(f.Business.Owner).Single();Assert.Null(legacy.RequestedReimbursementCents);Assert.Empty(legacy.History);
        var saved=book.SaveTrip(f.Business.Owner,legacy with{EvidenceId="Fictional first retained review after upgrade"});
        Assert.Equal(2,saved.History.Single().Revision);Assert.Equal(trip.Id,saved.Id);Assert.Null(saved.History.Single().RequestedReimbursementCents);
    }
}
