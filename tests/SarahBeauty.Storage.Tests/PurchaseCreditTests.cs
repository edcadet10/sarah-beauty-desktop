using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;

namespace SarahBeauty.Storage.Tests;

public sealed class PurchaseCreditTests
{
    private static PurchaseCredit Draft(LedgerFixture f,Purchase bill,DateOnly? date=null)=>new(){Name="Fictional supplier reduction",
        PurchaseId=bill.Id,Date=date??f.Date,Lines=bill.Lines.Select(l=>new PurchaseCreditLine(l.Id,l.GrossCents!.Value,l.TaxPaidCents)).ToArray()};

    private static PurchaseCredit Approve(LedgerFixture f,PurchaseCredit value)
    {
        var book=new PurchaseCreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,value);
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void UnpaidReductionChangesOnlyOperationalBalanceAndProtectsApprovedSources()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var book=new PurchaseCreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,bill));
        var credit=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(credit.Revision,book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Revision);
        Assert.Throws<ArgumentException>(()=>f.Post(SupplierPaymentTests.Payment(f,bill,1)));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute($"UPDATE Records SET DataJson=json_set(DataJson,'$.Name','Changed') WHERE Id='{credit.Id}';"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute($"DELETE FROM Records WHERE Id='{bill.Id}';"));
    }

    [Fact]
    public void PaidReductionShowsSupplierCreditUntilActualCashRefundArrives()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        Approve(f,Draft(f,bill,f.Date.AddDays(1)));
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(-35200,SupplierPaymentTests.Due(f,bill,f.Date.AddDays(1)));
        var position=Assert.Single(new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date.AddDays(1)).Suppliers);
        Assert.Equal(0,position.OpenCents);
        Assert.Equal(35200,position.CreditCents);
        Assert.Equal(-35200,f.Balance("1010"));
        f.Post(f.Draft(FinancialKind.PurchaseRefund,35200) with{OriginalId=payment.Id,Date=f.Date.AddDays(2)});
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill,f.Date.AddDays(2)));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance("5000"));
    }

    [Fact]
    public void PartialReductionKeepsTaxAndBusinessSharesExactForLaterPayment()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var line=bill.Lines[0];
        var credit=Approve(f,Draft(f,bill) with{Lines=[new(line.Id,1100,100,999999)]});
        Assert.Equal(550,credit.Lines.Single().BusinessCents);
        f.Post(SupplierPaymentTests.Payment(f,bill,34100));
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(550,f.Balance("5000"));
        Assert.Equal(33000,f.Balance("1500"));
        Assert.Equal(550,f.Balance("3100"));
    }

    [Fact]
    public void ChangedBalanceRequiresFreshCreditReview()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var book=new PurchaseCreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,bill));
        f.Post(SupplierPaymentTests.Payment(f,bill,1000));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal("Draft",book.List(f.Business.Owner).Single().Status);
    }

    [Fact]
    public void FailedCreditApprovalRollsBackAndRetriesWithOneReduction()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var book=new PurchaseCreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,bill));
        book.FailurePoint=_=>throw new IOException("Injected supplier credit failure");
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill));
        Assert.Equal("Draft",book.List(f.Business.Owner).Single().Status);
        book.FailurePoint=null;
        book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
    }

    [Fact]
    public void LinkedReversalRestoresTheExactReductionOnceAtItsOwnDate()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var credit=Approve(f,Draft(f,bill));
        var book=new PurchaseCreditBook(f.Business.Store);
        Approve(f,book.ReversalDraft(f.Business.Owner,credit.Id) with{Name="Fictional correction of supplier reduction",Date=f.Date.AddDays(1)});
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill,f.Date.AddDays(1)));
        Assert.Throws<ArgumentException>(()=>Approve(f,book.ReversalDraft(f.Business.Owner,credit.Id) with{Name="Fictional repeated reversal",Date=f.Date.AddDays(2)}));
    }

    [Fact]
    public void ANewCreditCannotUseValueRestoredOnlyByAFutureReversal()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var credit=Approve(f,Draft(f,bill));
        var book=new PurchaseCreditBook(f.Business.Store);
        Approve(f,book.ReversalDraft(f.Business.Owner,credit.Id) with{Name="Fictional later reversal",Date=f.Date.AddDays(2)});
        Assert.Throws<ArgumentException>(()=>Approve(f,Draft(f,bill,f.Date.AddDays(1))));
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill,f.Date.AddDays(1)));
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill,f.Date.AddDays(2)));
    }

    [Fact]
    public void UnresolvedTaxExcessAndForeignLinesCannotBeApproved()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var line=bill.Lines[0];
        foreach(var invalid in new[]{new PurchaseCreditLine(line.Id,2200,null),new(line.Id,2201,200),new(line.Id,2200,0),new(Guid.NewGuid().ToString(),100,0)})
            Assert.Throws<ArgumentException>(()=>Approve(f,Draft(f,bill) with{Lines=[invalid]}));
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
}
