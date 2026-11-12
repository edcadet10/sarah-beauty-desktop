using System.Globalization;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CancellationStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void MixedApprovalStoresGregorianRequestAndDecisionDates(string culture)
    {
        using var f=new LedgerFixture();var (book,draft)=Mixed(f);
        InCulture(culture,()=>book.Approve(f.Business.Owner,book.Preview(f.Business.Owner,draft),BusinessFixture.Phrase));
        Assert.Equal("2027-01-10",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT RequestedDate FROM CancellationDecisions;"));
        Assert.Equal("2027-01-11",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT DecisionDate FROM CancellationDecisions;"));
        var history=Assert.Single(book.History(f.Business.Owner));
        Assert.Equal(f.Date,history.RequestedDate);Assert.Equal(f.Date.AddDays(1),history.DecisionDate);
        Assert.Equal(7700,f.Balance("1010"));Assert.Equal(-7000,f.Balance("4000"));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ExistingHistoryAndExactApprovalRetryPreserveDatesSnapshotAndMoney(string culture)
    {
        using var f=new LedgerFixture();var (book,draft)=Mixed(f);
        var preview=book.Preview(f.Business.Owner,draft);var saved=book.Approve(f.Business.Owner,preview,BusinessFixture.Phrase);
        long watermark=Watermark(f);
        InCulture(culture,()=>
        {
            var history=Assert.Single(book.History(f.Business.Owner));
            Assert.Equal(f.Date,history.RequestedDate);Assert.Equal(f.Date.AddDays(1),history.DecisionDate);
            Assert.Equal(saved.Id,book.Approve(f.Business.Owner,preview,BusinessFixture.Phrase).Id);
            Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        });
        Assert.Equal(watermark,Watermark(f));Assert.Equal(7700,f.Balance("1010"));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void SourcesRetainTheOriginalReceiptReversalDate(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=invoice.BookingId});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=receipt.Id,Date=f.Date.AddDays(2)});
        long watermark=Watermark(f);
        InCulture(culture,()=>
        {
            var source=new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(3));
            Assert.Equal(f.Date.AddDays(2),Assert.Single(source.Payments).CancelledOn);
            Assert.Equal(0,Assert.Single(source.Payments).Available.Cents);
        });
        Assert.Equal(watermark,Watermark(f));Assert.Equal(0,f.Balance("1010"));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void SourcesKeepTheActualClosedBookBoundary(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);
        BookPeriodTests.Ready(f);BookPeriodTests.Close(f,new BookPeriods(f.Business.Store));
        long watermark=Watermark(f);
        InCulture(culture,()=>Assert.Equal(new DateOnly(2027,1,31),new CancellationSources(f.Business.Store)
            .Load(f.Business.Owner,invoice.BookingId,new(2027,2,1)).ClosedThrough));
        Assert.Equal(watermark,Watermark(f));
    }

    private static (CancellationCases Book,CancellationDraft Draft) Mixed(LedgerFixture f)
    {
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        var application=CancellationSourceTests.Apply(f,receipt.Id,invoice,11000);
        var book=new CancellationCases(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,CancellationPreviewTests.Draft(f,invoice) with
            {Unapplications=[new(application.Id,11000)],Reductions=[new(invoice.Id,[new(invoice.Snapshot.Lines[0].ChargeId,1)])],
            Values=[CancellationPreviewTests.Value(f,receipt.Id,retain:2200,refund:3300,tax:300,transfer:5500)]});
        return(book,draft);
    }

    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static void InCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
