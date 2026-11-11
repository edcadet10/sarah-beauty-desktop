using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class TripReimbursementSourceTests
{
    [Fact]
    public void CapturedReviewDistinguishesHistoricalPartialPaymentAndLaterRelease()
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);
        var linked=TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,5000));TripPaymentStorageTests.Release(f,book,linked,2000);
        var sources=new TripReimbursementSources(f.Business.Store);var earlier=sources.Load(f.Business.Owner,trip.Id,f.Date.AddDays(1));var later=sources.Load(f.Business.Owner,trip.Id,f.Date.AddDays(2));
        Assert.Equal(5000,earlier.CurrentClaimLinkedCents);Assert.Equal(3000,later.CurrentClaimLinkedCents);Assert.Equal(3000,earlier.Links.Single().RemainingAfterAllDatedActionsCents);
        Assert.Equal("TEST",earlier.Mode);Assert.Equal(trip.RequestedReimbursementCents,earlier.CurrentTrip.RequestedReimbursementCents);Assert.Single(earlier.Releases);
        Assert.Equal(0,earlier.OtherBasisLinkedCents);Assert.Equal(earlier.Watermark,later.Watermark);Assert.Single(f.Book.Posted(f.Business.Owner));
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>sources.Load(foreign.Owner,trip.Id,f.Date));
    }

    [Fact]
    public void EarlierClaimPaymentsDoNotBecomePaymentsOfANewerReviewedBasis()
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);
        var old=TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,5000));TripPaymentStorageTests.Release(f,book,old,5000);
        trip=new OperationalResources(f.Business.Store).SaveTrip(f.Business.Owner,trip with{ActualMiles=30.1m,RequestedReimbursementCents=7000});
        TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,7000,3));var sources=new TripReimbursementSources(f.Business.Store);
        var historical=sources.Load(f.Business.Owner,trip.Id,f.Date.AddDays(1));Assert.Equal(0,historical.CurrentClaimLinkedCents);Assert.Equal(5000,historical.OtherBasisLinkedCents);
        Assert.Equal(10000,historical.Links.Single(r=>!r.CurrentBasis).Source.Snapshot.Trip.RequestedReimbursementCents);
        var current=sources.Load(f.Business.Owner,trip.Id,f.Date.AddDays(3));Assert.Equal(7000,current.CurrentClaimLinkedCents);Assert.Equal(0,current.OtherBasisLinkedCents);
    }

    [Fact]
    public void ConcurrentNewLinkCannotMixTripRowsAndWatermarkGenerations()
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));f.Business.Files.Execute("PRAGMA journal_mode=WAL;");
        var book=new TripPayments(f.Business.Store);var sources=new TripReimbursementSources(f.Business.Store)
        {AfterTripRead=()=>TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,5000))};
        var old=sources.Load(f.Business.Owner,trip.Id,f.Date.AddDays(1));Assert.Empty(old.Links);Assert.Equal(0,old.CurrentClaimLinkedCents);sources.AfterTripRead=null;
        var changed=sources.Load(f.Business.Owner,trip.Id,f.Date.AddDays(1));Assert.Single(changed.Links);Assert.Equal(5000,changed.CurrentClaimLinkedCents);Assert.True(changed.Watermark>old.Watermark);
    }
}
