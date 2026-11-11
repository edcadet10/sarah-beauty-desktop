using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class AssetRefundTests
{
    private static PostedMovement Acquire(LedgerFixture f,bool depreciate=true)
    {
        var source=f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        if(depreciate)f.Post(f.Draft(FinancialKind.Depreciation,10000));
        return source;
    }

    private static FinancialDraft Refund(LedgerFixture f,PostedMovement source,long cents,string treatment)=>
        f.Draft(FinancialKind.SupplierRefund,cents) with{OriginalId=source.Id,CounterAccountId=source.Source.CounterAccountId,
            EvidenceId="Fictional asset refund decision",AssetAdjustments=[new(f.Asset.Id,treatment)]};

    [Fact]
    public void BasisRecoveryStopsAtCarryingValueAndSeparatesExcessRecovery()
    {
        using var f=new LedgerFixture();
        var source=Acquire(f);
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,source,25000,AssetValueChoice.Reduce)));
        var refund=f.Post(Refund(f,source,25000,AssetValueChoice.Recover));
        Assert.Equal(10000,f.Balance("1500"));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Equal(-5000,f.Balance("4010"));
        Assert.Equal(-5000,f.Balance("1010"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id});
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Equal(0,f.Balance("4010"));
    }

    [Theory]
    [InlineData(25000,5000,0)]
    [InlineData(15000,0,5000)]
    public void ReturnedAssetClearsCostAndDepreciationWithExactGainOrLoss(long cents,long gain,long loss)
    {
        using var f=new LedgerFixture();
        var source=Acquire(f);
        var refund=f.Post(Refund(f,source,cents,AssetValueChoice.Retire));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("1590"));
        Assert.Equal(-gain,f.Balance("4010"));
        Assert.Equal(loss,f.Balance("5060"));
        Assert.Equal(refund.Id,new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposalMovementId);
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id});
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
    }

    [Fact]
    public void InterruptedRetirementRollsBackMoneyAssetStateAndReviewGeneration()
    {
        using var f=new LedgerFixture();
        var source=Acquire(f);
        var draft=f.Book.SaveDraft(f.Business.Owner,Refund(f,source,25000,AssetValueChoice.Retire));
        f.Book.FailurePoint=p=>{if(p=="asset-refund-retirement")throw new IOException("Injected asset refund failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Equal(-30000,f.Balance("1010"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        f.Book.FailurePoint=null;
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(0,f.Balance("1500"));
    }

    [Fact]
    public void NewDepreciationRequiresFreshRecoveryReview()
    {
        using var f=new LedgerFixture();
        var source=Acquire(f);
        var draft=f.Book.SaveDraft(f.Business.Owner,Refund(f,source,25000,AssetValueChoice.Recover));
        f.Post(f.Draft(FinancialKind.Depreciation,1000));
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        draft=f.Book.SaveDraft(f.Business.Owner,draft);
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(-6000,f.Balance("4010"));
    }

    [Fact]
    public void LaterRecoveryMustBeReversedBeforeUndoingThePhysicalRetirement()
    {
        using var f=new LedgerFixture();
        var source=Acquire(f);
        var returned=f.Post(Refund(f,source,20000,AssetValueChoice.Retire));
        var recovery=f.Post(Refund(f,source,10000,AssetValueChoice.Recover) with{Date=f.Date.AddDays(1)});
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=returned.Id,Date=f.Date.AddDays(1)}));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=recovery.Id,Date=f.Date.AddDays(2)});
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=returned.Id,Date=f.Date.AddDays(1)}));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=returned.Id,Date=f.Date.AddDays(2)});
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
    }

    [Fact]
    public void KnownZeroBookValueCanRetireAndReverseWithoutInventingMoney()
    {
        using var f=new LedgerFixture();
        var source=Acquire(f,depreciate:false);
        f.Post(Refund(f,source,30000,AssetValueChoice.Reduce));
        var retired=f.Post(f.Draft(FinancialKind.AssetDisposal,0) with{AssetId=f.Asset.Id,EvidenceId="Fictional zero-value retirement"});
        Assert.Empty(f.Book.Journal(f.Business.Owner,retired.Id));
        Assert.NotNull(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=retired.Id});
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance("1500"));
    }

    [Fact]
    public void AnUnrelatedAssetChoiceOrContraAssetAcquisitionCannotPost()
    {
        using var f=new LedgerFixture();
        var source=Acquire(f);
        var other=new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,new Asset{Name="Fictional unrelated asset",BusinessUseFraction=1m});
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,source,1000,AssetValueChoice.Retire) with{AssetAdjustments=[new(other.Id,AssetValueChoice.Retire)]}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.AssetPurchase,1000) with{CounterAccountId=f.Accounts["1590"].Id}));
        Assert.All(new BusinessResources(f.Business.Store).Assets(f.Business.Owner),a=>Assert.Null(a.DisposedDate));
    }
}
