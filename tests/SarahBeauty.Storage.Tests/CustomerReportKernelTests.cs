using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CustomerReportKernelTests
{
    [Fact]
    public void CapturedAgingAndFundedValuesMatchHumanReportsWithoutRenewingLockedAccess()
    {
        using var f=new LedgerFixture();
        var asOf=f.Date.AddDays(10);
        var overdue=ApplicationTests.Invoice(f,1000,dueDate:asOf.AddDays(-1));
        ApplicationTests.Invoice(f,2000,dueDate:asOf);
        ApplicationTests.Invoice(f,3000,dueDate:asOf.AddDays(1));
        ApplicationTests.Invoice(f,4000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,15000));
        var credits=new CreditBook(f.Business.Store);
        var draft=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional funded customer report credit",PaymentId=receipt.Id,Cents=6000,Date=f.Date});
        credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Receipt,4000) with{CounterAccountId=f.Accounts["2200"].Id});
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,asOf);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        CancellationSourceTests.Apply(f,receipt.Id,overdue,500);
        f.Business.Owner.Lock();
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var actual=source.Read(db=>CustomerPositions.Read(db,source.Source.BusinessId,asOf,DocumentVersions.List(db,source.Source.BusinessId)));
        Assert.Equal(expected.Customers,actual);
        var row=Assert.Single(actual);
        Assert.Equal(10000,row.OpenCents);
        Assert.Equal(1000,row.OverdueCents);
        Assert.Equal(2000,row.DueTodayCents);
        Assert.Equal(3000,row.FutureCents);
        Assert.Equal(4000,row.UnknownDueCents);
        Assert.Equal(9000,row.UnallocatedReceiptsCents);
        Assert.Equal(6000,row.UnusedCreditCents);
        Assert.Equal(4000,row.HeldReceiptsCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));
        Assert.Throws<UnauthorizedAccessException>(()=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,asOf));
    }

    [Fact]
    public void ReadOnlyBusinessScopeCannotFindAnotherBusinessSource()
    {
        using var f=new LedgerFixture();using var other=new BusinessFixture();
        var invoice=ApplicationTests.Invoice(f);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        string foreign=other.Owner.BusinessId;
        source.Read(db=>
        {
            Assert.Null(CustomerBook.Find(db,foreign,f.CustomerId));
            Assert.Null(DocumentVersions.Find(db,foreign,id:invoice.Id));
            Assert.Empty(DocumentVersions.List(db,foreign));
            Assert.Empty(CreditValues.List(db,foreign));
            Assert.Throws<ArgumentException>(()=>ValueAvailability.Payment(db,foreign,payment.Id,f.Date));
            Assert.Throws<ArgumentException>(()=>ValueAvailability.Invoice(db,foreign,invoice.Id,f.Date));
            return true;
        });
    }
}
