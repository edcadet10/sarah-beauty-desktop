using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class DisposalTests
{
    private static FinancialDraft Draft(LedgerFixture f,long proceeds)=>f.Draft(FinancialKind.AssetDisposal,proceeds) with
    {AssetId=f.Asset.Id,EvidenceId="Fictional approved disposal evidence",CustomerId=proceeds>0?f.CustomerId:""};

    [Theory]
    [InlineData(25000,5000,0)]
    [InlineData(15000,0,5000)]
    [InlineData(0,0,20000)]
    public void DisposalRemovesCostAndDepreciationWithExactGainOrLoss(long proceeds,long gain,long loss)
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var disposal=f.Post(Draft(f,proceeds));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("1590"));
        Assert.Equal(-gain,f.Balance("4010"));
        Assert.Equal(loss,f.Balance("5060"));
        var asset=new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single();
        Assert.Equal(disposal.Id,asset.DisposalMovementId);
        Assert.Equal(f.Date,asset.DisposedDate);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Depreciation,1)));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=disposal.Id,Date=f.Date.AddDays(1)});
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
    }

    [Fact]
    public void StaleDisposalAndUnknownBookValueAreRejected()
    {
        using var f=new LedgerFixture();
        Assert.Throws<ArgumentException>(()=>f.Post(Draft(f,0)));
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        var draft=f.Book.SaveDraft(f.Business.Owner,Draft(f,20000));
        f.Post(f.Draft(FinancialKind.Depreciation,5000));
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        draft=f.Book.SaveDraft(f.Business.Owner,draft);
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(5000,f.Balance("5060"));
    }

    [Fact]
    public void FailedDisposalRollsBackLedgerAndAssetStateBeforeRetry()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        var draft=f.Book.SaveDraft(f.Business.Owner,Draft(f,20000));
        f.Book.FailurePoint=p=>{if(p=="asset-disposal-record")throw new IOException("Injected disposal failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
        Assert.Equal(30000,f.Balance("1500"));
        f.Book.FailurePoint=null;
        var posted=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(posted.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
    }

    [Fact]
    public void ExplicitDisposalTaxIsSeparateFromGainAndRemittanceBlocksAnUnsafeReversal()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var disposal=f.Post(Draft(f,33000) with{TaxCents=3000});
        Assert.Equal(-10000,f.Balance("4010"));
        Assert.Equal(-3000,f.Balance("2000"));
        Assert.Equal(3000,new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date).Tax.CollectedCents);
        var receipts=new ReceiptBook(f.Business.Store);
        var receipt=receipts.SaveDraft(f.Business.Owner,new ReceiptDraft{Name="Fictional asset sale acknowledgment",PaymentId=disposal.Id,IssueDate=f.Date});
        Assert.Equal(33000,receipts.Issue(f.Business.Owner,receipt.Id,receipt.Revision,BusinessFixture.Phrase).Snapshot.TotalCents);
        f.Post(f.Draft(FinancialKind.TaxRemittance,3000));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=disposal.Id}));
        Assert.NotNull(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);
    }

    [Fact]
    public void DisposalCannotPrecedeALaterRecordedAssetMovement()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.Depreciation,10000) with{Date=f.Date.AddDays(1)});
        Assert.Throws<ArgumentException>(()=>f.Post(Draft(f,20000)));
        Assert.Equal(30000,f.Balance("1500"));
    }

    [Fact]
    public void AFutureReversalDoesNotPermitPostingDuringTheDisposedInterval()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        var disposal=f.Post(Draft(f,20000));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=disposal.Id,Date=f.Date.AddDays(2)});
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.AssetPurchase,1000) with{Date=f.Date.AddDays(1)}));
        f.Post(f.Draft(FinancialKind.AssetPurchase,1000) with{Date=f.Date.AddDays(2)});
        Assert.Equal(31000,f.Balance("1500"));
    }
}
