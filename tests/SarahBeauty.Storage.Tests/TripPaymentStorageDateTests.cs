using System.Globalization;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class TripPaymentStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void NewLinkAndReleaseStoreGregorianDatesWithoutPostingMoreMoney(string culture)
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);
        var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);
        InCulture(culture,()=>
        {
            var link=TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,4000));
            TripPaymentStorageTests.Release(f,book,link,1000);
        });
        Assert.Equal("2027-01-11",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Date FROM TripPaymentLinks;"));
        Assert.Equal("2027-01-12",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Date FROM TripPaymentReleases;"));
        Assert.Equal(3000,new TripReimbursementSources(f.Business.Store).Load(f.Business.Owner,trip.Id,f.Date.AddDays(2)).CurrentClaimLinkedCents);
        Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(-10000,f.Balance("1010"));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ExistingHistoryAndExactRetriesPreserveSnapshotDatesAndWatermark(string culture)
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));
        var book=new TripPayments(f.Business.Store);var draft=TripPaymentStorageTests.Draft(f,trip,payment,4000);
        var preview=book.Preview(f.Business.Owner,draft);string request=Guid.NewGuid().ToString();
        var link=book.Approve(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase);
        var releaseDraft=new TripPaymentReleaseDraft(link.Id,f.Date.AddDays(2),1000,"Fictional corrected trip attribution","Fictional retained review evidence");
        var releasePreview=book.PreviewRelease(f.Business.Owner,releaseDraft);string releaseRequest=Guid.NewGuid().ToString();
        var release=book.Release(f.Business.Owner,releaseDraft,releasePreview.Watermark,releaseRequest,BusinessFixture.Phrase);long watermark=Watermark(f);
        InCulture(culture,()=>
        {
            Assert.Equal(link.SnapshotHash,Assert.Single(book.History(f.Business.Owner)).SnapshotHash);
            Assert.Equal(release.SnapshotHash,Assert.Single(book.ReleaseHistory(f.Business.Owner)).SnapshotHash);
            Assert.Equal(link.Id,book.Approve(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase).Id);
            Assert.Equal(release.Id,book.Release(f.Business.Owner,releaseDraft,releasePreview.Watermark,releaseRequest,BusinessFixture.Phrase).Id);
        });
        Assert.Equal(watermark,Watermark(f));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void HistoricalPaymentAvailabilityKeepsALaterSupplierRefundInItsActualPeriod(string culture)
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));
        f.Post(f.Draft(FinancialKind.SupplierRefund,3000) with{OriginalId=payment.Id,Date=f.Date.AddDays(3)});
        var book=new TripPayments(f.Business.Store);var draft=TripPaymentStorageTests.Draft(f,trip,payment,7000);
        InCulture(culture,()=>
        {
            Assert.Equal(10000,book.Preview(f.Business.Owner,draft).PaymentAvailableCents);
            Assert.Equal(7000,book.Preview(f.Business.Owner,draft with{Date=f.Date.AddDays(3)}).PaymentAvailableCents);
            Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Cents=7001}));
        });
        Assert.Empty(book.History(f.Business.Owner));Assert.Equal(-7000,f.Balance("1010"));
    }

    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static void InCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
