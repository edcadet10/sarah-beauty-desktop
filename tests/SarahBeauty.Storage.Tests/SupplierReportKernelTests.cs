using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class SupplierReportKernelTests
{
    [Fact]
    public void CapturedAgingAndSupplierCreditMatchHumanReportsAfterLaterPaymentAndLock()
    {
        using var f=new LedgerFixture();
        var asOf=f.Date.AddDays(10);
        var supplier=new BusinessResources(f.Business.Store).SaveSupplier(f.Business.Owner,new(){Name="Fictional scoped report supplier"});
        var purchases=new PurchaseBook(f.Business.Store);
        Purchase Bill(long cents,DateOnly? due)
        {
            var value=purchases.SaveDraft(f.Business.Owner,new(){Name="Fictional scoped report bill",SupplierId=supplier.Id,PurchaseDate=f.Date,DueDate=due,Lines=[new(Guid.NewGuid().ToString(),"Fictional service",1,cents,0,1m,f.Accounts["5010"].Id)]});
            return purchases.Approve(f.Business.Owner,value.Id,value.Revision,BusinessFixture.Phrase);
        }
        var overdue=Bill(1000,asOf.AddDays(-1));Bill(2000,asOf);Bill(3000,asOf.AddDays(1));Bill(4000,null);
        f.Post(SupplierPaymentTests.Payment(f,overdue,500));
        var funded=Bill(2000,null);f.Post(SupplierPaymentTests.Payment(f,funded,2000));
        var credits=new PurchaseCreditBook(f.Business.Store);
        var credit=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional supplier report credit",PurchaseId=funded.Id,Date=f.Date,Lines=[new(funded.Lines[0].Id,500,0)]});
        credits.Approve(f.Business.Owner,credit.Id,credit.Revision,BusinessFixture.Phrase);
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,asOf);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(SupplierPaymentTests.Payment(f,overdue,500));f.Business.Owner.Lock();
        var actual=source.Read(db=>
        {
            var bills=PurchasePayments.Outstanding(db,source.Source.BusinessId,asOf);
            Assert.Equal(expected.SupplierBills,bills);
            return SupplierPositions.Read(db,source.Source.BusinessId,asOf,bills);
        });
        Assert.Equal(expected.Suppliers,actual);
        var row=Assert.Single(actual);
        Assert.Equal(9500,row.OpenCents);Assert.Equal(500,row.OverdueCents);Assert.Equal(2000,row.DueTodayCents);
        Assert.Equal(3000,row.FutureCents);Assert.Equal(4000,row.UnknownDueCents);Assert.Equal(500,row.CreditCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>new PurchasePayments(f.Business.Store).Outstanding(f.Business.Owner,asOf));
    }

    [Fact]
    public void ScopedRecordAndBillReadersReturnNoForeignBusinessSources()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();
        var bill=SupplierPaymentTests.Bill(f);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        source.Read(db=>
        {
            Assert.Empty(RecordStore<Purchase>.ReadBusiness(db,foreign.Owner.BusinessId,"Purchase"));
            Assert.Empty(PurchasePayments.Outstanding(db,foreign.Owner.BusinessId,f.Date));
            Assert.Empty(PurchaseCredits.Approved(db,foreign.Owner.BusinessId,bill.Id,f.Date));
            Assert.Empty(SupplierPositions.Read(db,foreign.Owner.BusinessId,f.Date,[]));
            return true;
        });
    }
}
