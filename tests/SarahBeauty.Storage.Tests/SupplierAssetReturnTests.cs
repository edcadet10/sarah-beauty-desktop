using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class SupplierAssetReturnTests
{
    private static FinancialDraft Refund(LedgerFixture f,PostedMovement payment,string line,long cents,string treatment)=>
        f.Draft(FinancialKind.PurchaseRefund,cents) with{OriginalId=payment.Id,EvidenceId="Fictional equipment return decision",
            SupplierRefundPortions=[new(line,cents)],AssetAdjustments=[new(f.Asset.Id,treatment)]};

    [Fact]
    public void EquipmentOnlyReturnPreservesSuppliesAndRestoresExactSourceOnReversal()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var paid=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var refund=f.Post(Refund(f,paid,bill.Lines[1].Id,25000,AssetValueChoice.Retire));
        Assert.Equal(1100,f.Balance("5000"));
        Assert.Equal(1100,f.Balance("3100"));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("1590"));
        Assert.Equal(-2000,f.Balance("4010"));
        Assert.Equal(-10200,f.Balance("1010"));
        Assert.Equal(25000,SupplierPaymentTests.Due(f,bill));
        var available=new PurchasePayments(f.Business.Store).RefundableLines(f.Business.Owner,paid.Id,f.Date);
        Assert.Equal(2200,available.Single(l=>l.LineId==bill.Lines[0].Id).RemainingCents);
        Assert.Equal(8000,available.Single(l=>l.LineId==bill.Lines[1].Id).RemainingCents);
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id});
        Assert.Equal(33000,f.Balance("1500"));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Equal(0,f.Balance("4010"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
    }

    [Fact]
    public void SelectedLineAmountsMustBeDistinctAvailableAndEqualTheRefundTotal()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var paid=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var draft=f.Draft(FinancialKind.PurchaseRefund,1000) with{OriginalId=paid.Id};
        foreach(var choices in new SupplierRefundPortion[][]{
            [new("foreign",1000)], [new(bill.Lines[0].Id,999)], [new(bill.Lines[0].Id,-1)],
            [new(bill.Lines[0].Id,500),new(bill.Lines[0].Id,500)]})
            Assert.Throws<ArgumentException>(()=>f.Post(draft with{SupplierRefundPortions=choices}));
        Assert.Throws<ArgumentException>(()=>f.Post(draft with{AmountCents=2201,SupplierRefundPortions=[new(bill.Lines[0].Id,2201)]}));
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,paid,bill.Lines[0].Id,1000,AssetValueChoice.Retire)));
        Assert.Equal(-35200,f.Balance("1010"));
        Assert.Equal(33000,f.Balance("1500"));
    }

    [Fact]
    public void ReturningOneAssetAcrossSeveralSourceLinesRemovesItsBookValueOnlyOnce()
    {
        using var f=new LedgerFixture();
        var value=PurchaseTests.Draft(f);
        var equipment=value.Lines[1] with{UnitCostCents=15000,TaxPaidCents=0};
        var bill=SupplierPaymentTests.Bill(f,value with{Lines=[equipment,equipment with{Id=Guid.NewGuid().ToString()}]});
        var paid=f.Post(SupplierPaymentTests.Payment(f,bill,30000));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var returned=f.Post(f.Draft(FinancialKind.PurchaseRefund,25000) with{OriginalId=paid.Id,EvidenceId="Fictional multi-line asset return",
            SupplierRefundPortions=[new(bill.Lines[0].Id,15000),new(bill.Lines[1].Id,10000)],AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Retire)]});
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("1590"));
        Assert.Equal(-5000,f.Balance("4010"));
        Assert.Equal(returned.Id,new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposalMovementId);
    }

    private static FinancialDraft CreditedAsset(LedgerFixture f,out Purchase target)
    {
        var bill=SupplierPaymentTests.Bill(f);
        var paid=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var credits=new PurchaseCreditBook(f.Business.Store);
        var credit=credits.SaveDraft(f.Business.Owner,new PurchaseCredit{Name="Fictional full equipment reduction",Date=f.Date,PurchaseId=bill.Id,
            Lines=[new(bill.Lines[1].Id,33000,3000)]});
        credits.Approve(f.Business.Owner,credit.Id,credit.Revision,BusinessFixture.Phrase);
        target=SupplierPaymentTests.Bill(f,new Purchase{Name="Fictional replacement supplies",SupplierId=bill.SupplierId,PurchaseDate=f.Date,
            Lines=[new(Guid.NewGuid().ToString(),"Fictional supplies",1,33000,0,1m,f.Accounts["5000"].Id)]});
        return f.Draft(FinancialKind.SupplierCreditUse,33000) with{OriginalId=paid.Id,PurchaseId=target.Id,EvidenceId="Fictional return paid as supplier credit",
            AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Retire)]};
    }

    [Fact]
    public void CreditForReturnedEquipmentFundsReplacementWithoutAnotherCashPayment()
    {
        using var f=new LedgerFixture();
        var draft=CreditedAsset(f,out var target);
        var applied=f.Post(draft);
        Assert.Equal(34100,f.Balance("5000"));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("1590"));
        Assert.Equal(-10000,f.Balance("4010"));
        Assert.Equal(-35200,f.Balance("1010"));
        Assert.Equal(0,SupplierPaymentTests.Due(f,target));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=applied.Id});
        Assert.Equal(1100,f.Balance("5000"));
        Assert.Equal(33000,f.Balance("1500"));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Equal(33000,SupplierPaymentTests.Due(f,target));
    }

    [Fact]
    public void APhysicallyReturnedAssetCannotReceiveItsOwnReplacementCost()
    {
        using var f=new LedgerFixture();
        var draft=CreditedAsset(f,out var target);
        var replacement=SupplierPaymentTests.Bill(f,new Purchase{Name="Fictional incorrectly reused asset",SupplierId=target.SupplierId,PurchaseDate=f.Date,
            Lines=[target.Lines[0] with{AccountId=f.Accounts["1500"].Id,AssetId=f.Asset.Id}]});
        Assert.Throws<ArgumentException>(()=>f.Post(draft with{PurchaseId=replacement.Id}));
        Assert.Equal(33000,f.Balance("1500"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
    }

    [Fact]
    public void MixedBillReturnRollsBackSettlementAndRetirementAndRejectsStaleAssetReview()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var paid=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var draft=f.Book.SaveDraft(f.Business.Owner,Refund(f,paid,bill.Lines[1].Id,25000,AssetValueChoice.Recover));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        draft=f.Book.SaveDraft(f.Business.Owner,draft with{AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Retire)]});
        f.Book.FailurePoint=p=>{if(p=="purchase-settlement")throw new IOException("Injected mixed asset return failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(33000,f.Balance("1500"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        f.Book.FailurePoint=null;
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(25000,SupplierPaymentTests.Due(f,bill));
    }
}
