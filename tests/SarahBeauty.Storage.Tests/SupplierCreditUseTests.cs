using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class SupplierCreditUseTests
{
    private static Purchase Bill(LedgerFixture f,long cents,decimal use=1m,string? supplier=null,string? lineId=null)
    {
        supplier??=new BusinessResources(f.Business.Store).SaveSupplier(f.Business.Owner,new Supplier{Name="Fictional credit supplier"}).Id;
        return SupplierPaymentTests.Bill(f,new Purchase{Name="Fictional supplier-credit bill",SupplierId=supplier,PurchaseDate=f.Date,
            Lines=[new(lineId??Guid.NewGuid().ToString(),"Fictional credit purchase",1,cents,0,use,f.Accounts["5000"].Id)]});
    }

    private static void Credit(LedgerFixture f,Purchase bill)
    {
        var book=new PurchaseCreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new PurchaseCredit{Name="Fictional returned-purchase credit",PurchaseId=bill.Id,Date=f.Date,
            Lines=bill.Lines.Select(l=>new PurchaseCreditLine(l.Id,l.GrossCents!.Value,l.TaxPaidCents)).ToArray()});
        book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static FinancialDraft Use(LedgerFixture f,PostedMovement payment,Purchase target,long cents,string treatment="Original funding")=>
        f.Draft(FinancialKind.SupplierCreditUse,cents) with{OriginalId=payment.Id,PurchaseId=target.Id,
            EconomicOwnerId=f.Business.Owner.OwnerId,SupplierCreditTreatment=treatment,CashAccountId=""};

    [Fact]
    public void CreditPaysAnotherBillWithoutAnotherCashMovementOrExpense()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000));
        Credit(f,source);
        var target=Bill(f,20000,supplier:source.SupplierId,lineId:source.Lines[0].Id);
        var applied=f.Post(Use(f,payment,target,10000));
        Assert.Equal(0,SupplierPaymentTests.Due(f,source));
        Assert.Equal(10000,SupplierPaymentTests.Due(f,target));
        Assert.Equal(-10000,f.Balance("1010"));
        Assert.Equal(10000,f.Balance("5000"));
        Assert.Equal(0,f.Balance("4000"));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.PurchaseRefund,1) with{OriginalId=payment.Id}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id}));
        f.Post(SupplierPaymentTests.Payment(f,target,10000));
        Assert.Equal(0,SupplierPaymentTests.Due(f,target));
        Assert.Equal(20000,f.Balance("5000"));
        Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,$"SELECT COUNT(*) FROM PurchaseSettlements WHERE MovementId='{applied.Id}';"));
    }

    [Theory]
    [InlineData("Owner contribution","3000",0.4,4000)]
    [InlineData("Owner contribution","3000",0.6,6000)]
    [InlineData("Owner reimbursable","2100",0.4,4000)]
    [InlineData("Owner reimbursable","2100",0.6,6000)]
    public void DifferentBusinessUseAdjustsOriginalOwnerFundingExactly(string funding,string code,double targetUse,long expected)
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000,0.5m);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000,funding));
        Credit(f,source);
        var target=Bill(f,10000,(decimal)targetUse,source.SupplierId);
        f.Post(Use(f,payment,target,10000));
        Assert.Equal(expected,f.Balance("5000"));
        Assert.Equal(-expected,f.Balance(code));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance("3100"));
    }

    [Fact]
    public void ReimbursedOwnerCanRetainLowerBusinessUseAsDrawAndBankRefundClearsThatDraw()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000,0.5m);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000,"Owner reimbursable"));
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,5000));
        Credit(f,source);
        var target=Bill(f,10000,0.4m,source.SupplierId);
        Assert.Throws<ArgumentException>(()=>f.Post(Use(f,payment,target,10000)));
        var applied=f.Post(Use(f,payment,target,10000,"Owner withdrawal"));
        Assert.Equal(1000,f.Balance("3100"));
        Assert.Equal(0,f.Balance("2100"));
        f.Post(f.Draft(FinancialKind.PurchaseRefund,10000) with{OriginalId=applied.Id});
        Assert.Equal(0,f.Balance("3100"));
        Assert.Equal(-5000,f.Balance("2100"));
        Assert.Equal(5000,f.Balance("1010"));
        Assert.Equal(0,f.Balance("5000"));
    }

    [Fact]
    public void ChainedCreditUsesRestorePriorDrawBeforeAddingFurtherBusinessFunding()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000,0.5m);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000,"Owner reimbursable"));
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,5000));
        Credit(f,source);
        var second=Bill(f,10000,0.4m,source.SupplierId);
        var firstUse=f.Post(Use(f,payment,second,10000,"Owner withdrawal"));
        Credit(f,second);
        var third=Bill(f,10000,1m,source.SupplierId);
        f.Post(Use(f,firstUse,third,10000));
        Assert.Equal(10000,f.Balance("5000"));
        Assert.Equal(0,f.Balance("3100"));
        Assert.Equal(-5000,f.Balance("2100"));
        Assert.Equal(-5000,f.Balance("1010"));
        Assert.Equal(0,SupplierPaymentTests.Due(f,source));
        Assert.Equal(0,SupplierPaymentTests.Due(f,second));
        Assert.Equal(0,SupplierPaymentTests.Due(f,third));
    }

    [Fact]
    public void CreditCanMoveBetweenReturnedAndUnpaidLinesOfTheSameBill()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000);
        var mixed=SupplierPaymentTests.Bill(f,new Purchase{Name="Fictional two-line bill",SupplierId=source.SupplierId,PurchaseDate=f.Date,
            Lines=[new(Guid.NewGuid().ToString(),"Fictional returned line",1,10000,0,1m,f.Accounts["5010"].Id),
                new(Guid.NewGuid().ToString(),"Fictional retained line",1,10000,0,1m,f.Accounts["5000"].Id)]});
        var payment=f.Post(SupplierPaymentTests.Payment(f,mixed,10000));
        var credits=new PurchaseCreditBook(f.Business.Store);
        var draft=credits.SaveDraft(f.Business.Owner,new PurchaseCredit{Name="Fictional one-line return",PurchaseId=mixed.Id,Date=f.Date,Lines=[new(mixed.Lines[0].Id,10000,0)]});
        credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var before=new PurchasePayments(f.Business.Store).Outstanding(f.Business.Owner,f.Date).Single(p=>p.PurchaseId==mixed.Id);
        Assert.Equal(5000,before.PayableCents);
        Assert.Equal(5000,before.CreditCents);
        f.Post(Use(f,payment,mixed,5000));
        var after=new PurchasePayments(f.Business.Store).Outstanding(f.Business.Owner,f.Date).Single(p=>p.PurchaseId==mixed.Id);
        Assert.Equal(0,after.PayableCents);
        Assert.Equal(0,after.CreditCents);
        Assert.Equal(0,f.Balance("5010"));
        Assert.Equal(10000,f.Balance("5000"));
    }

    [Fact]
    public void ReversalRestoresBothBillBalancesAndCanRetryOnlyOnce()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000));
        Credit(f,source);
        var target=Bill(f,10000,supplier:source.SupplierId);
        var applied=f.Post(Use(f,payment,target,10000));
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Reversal,0) with{OriginalId=applied.Id,Date=f.Date.AddDays(1)});
        var reversed=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(reversed.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(-10000,SupplierPaymentTests.Due(f,source,f.Date.AddDays(1)));
        Assert.Equal(10000,SupplierPaymentTests.Due(f,target,f.Date.AddDays(1)));
        Assert.Equal(-10000,f.Balance("1010"));
    }

    [Fact]
    public void CrossBillFailureRollsBackBothSidesAndTheirSourceAvailability()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000));
        Credit(f,source);
        var target=Bill(f,10000,supplier:source.SupplierId);
        var draft=f.Book.SaveDraft(f.Business.Owner,Use(f,payment,target,10000));
        f.Book.FailurePoint=p=>{if(p=="purchase-settlement")throw new IOException("Injected cross-bill credit failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(-10000,SupplierPaymentTests.Due(f,source));
        Assert.Equal(10000,SupplierPaymentTests.Due(f,target));
        f.Book.FailurePoint=null;
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(0,SupplierPaymentTests.Due(f,target));
    }

    [Fact]
    public void TableMigrationPreservesSourceConsumptionAndReversalReferences()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000));
        Credit(f,source);
        var target=Bill(f,10000,supplier:source.SupplierId);
        var applied=f.Post(Use(f,payment,target,10000));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=applied.Id});
        f.Business.Files.Execute("PRAGMA foreign_keys=ON;"+SupplierTransferSchema.Sql);
        Assert.Null(TestStore.Scalar(f.Business.Files.DatabasePath,"PRAGMA foreign_key_check;"));
        Assert.Equal(-10000,SupplierPaymentTests.Due(f,source));
        Assert.Equal(10000,SupplierPaymentTests.Due(f,target));
        Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM PurchaseSettlements WHERE ConsumesId IS NOT NULL;"));
    }

    [Fact]
    public async Task CompetingUsesCannotSpendTheSameSupplierCreditTwice()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,32665);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,32665));
        Credit(f,source);
        var targets=new[]{Bill(f,20000,supplier:source.SupplierId),Bill(f,20000,supplier:source.SupplierId)};
        var drafts=targets.Select(t=>f.Book.SaveDraft(f.Business.Owner,Use(f,payment,t,20000))).ToArray();
        var results=await Task.WhenAll(drafts.Select(d=>Task.Run(()=>
        {
            try{f.Book.Approve(f.Business.Owner,d.Id,d.Revision,BusinessFixture.Phrase);return true;}
            catch(ArgumentException){return false;}
        })));
        Assert.Single(results,r=>r);
        Assert.Equal(-12665,SupplierPaymentTests.Due(f,source));
        Assert.Equal(-32665,f.Balance("1010"));
    }

    [Fact]
    public void UnfundedReductionAndAnotherSuppliersBillCannotConsumeReceivedValue()
    {
        using var f=new LedgerFixture();
        var source=Bill(f,10000);
        var payment=f.Post(SupplierPaymentTests.Payment(f,source,10000));
        var target=Bill(f,10000,supplier:source.SupplierId);
        Assert.Throws<ArgumentException>(()=>f.Post(Use(f,payment,target,10000)));
        Credit(f,source);
        var otherSupplier=Bill(f,10000);
        Assert.Throws<ArgumentException>(()=>f.Post(Use(f,payment,otherSupplier,10000)));
        f.Post(f.Draft(FinancialKind.PurchaseRefund,10000) with{OriginalId=payment.Id});
        Assert.Throws<ArgumentException>(()=>f.Post(Use(f,payment,target,1)));
        Assert.Equal(10000,SupplierPaymentTests.Due(f,target));
        Assert.Equal(0,f.Balance("1010"));
    }
}
