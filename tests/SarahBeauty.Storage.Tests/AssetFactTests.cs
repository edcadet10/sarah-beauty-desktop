using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class AssetFactTests
{
    private static AssetFactDraft Draft(LedgerFixture f,long? cost=30000,long? basis=25000)=>new(f.Asset.Id,cost,basis,"Fictional confirmed register facts","Fictional acquisition and tax review evidence");

    [Fact]
    public void UnknownRegisteredFactsCanBeCompletedAndCorrectedWithoutChangingApprovedBookValues()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.AssetPurchase,30000));var facts=new AssetFacts(f.Business.Store);var owner=f.Business.Owner;
        Assert.True(facts.RequiresReview(owner,f.Asset.Id));Assert.Throws<ArgumentException>(()=>new BusinessResources(f.Business.Store).SaveAsset(owner,f.Asset with{CostCents=30000,TaxBasisCents=25000,EvidenceId="Fictional evidence"}));
        var first=facts.Review(owner,Draft(f),f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var second=facts.Review(owner,Draft(f,basis:24000),first.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Equal(2,second.FactHistory.Length);Assert.Null(second.FactHistory[0].PreviousCostCents);Assert.Equal(25000,second.FactHistory[1].PreviousTaxBasisCents);
        Assert.Equal(24000,second.TaxBasisCents);Assert.Equal(30000,f.Balance("1500"));Assert.Equal(-30000,f.Balance("1010"));Assert.Single(f.Book.Posted(owner));
        Assert.All(second.FactHistory,h=>{Assert.Equal(owner.OwnerId,h.OwnerId);Assert.NotEmpty(h.AtUtc);});
        Assert.DoesNotContain(new FinancialReports(f.Business.Store).Load(owner,f.Date,f.Date).OpeningWarnings,w=>w.StartsWith(f.Asset.Name+":"));
        Assert.Equal(second.FactHistory,new BusinessResources(f.Business.Store).Assets(owner).Single().FactHistory);
    }

    [Fact]
    public void ZeroUnknownAndRepeatedRequestsRemainDistinctWithoutReapplyingOldFacts()
    {
        using var f=new LedgerFixture();var facts=new AssetFacts(f.Business.Store);string request=Guid.NewGuid().ToString();var draft=Draft(f,cost:0,basis:0);
        var zero=facts.Review(f.Business.Owner,draft,f.Asset.Revision,request,BusinessFixture.Phrase);Assert.Equal(0,zero.CostCents);Assert.Equal(0,zero.TaxBasisCents);
        var unknown=facts.Review(f.Business.Owner,Draft(f,null,null),zero.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);Assert.Null(unknown.CostCents);Assert.Null(unknown.TaxBasisCents);
        var retry=facts.Review(f.Business.Owner,draft,f.Asset.Revision,request,BusinessFixture.Phrase);Assert.Null(retry.CostCents);Assert.Equal(2,retry.FactHistory.Length);
        Assert.Throws<ArgumentException>(()=>facts.Review(f.Business.Owner,draft with{TaxBasisCents=1},f.Asset.Revision,request,BusinessFixture.Phrase));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void OrdinaryEditsCannotReplaceReviewsAndInvalidOrStaleReviewsCannotApprove()
    {
        using var f=new LedgerFixture();var facts=new AssetFacts(f.Business.Store);var owner=f.Business.Owner;
        Assert.Throws<ArgumentException>(()=>facts.Review(owner,Draft(f,cost:-1),f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>facts.Review(owner,Draft(f) with{EvidenceId=""},f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Throws<UnauthorizedAccessException>(()=>facts.Review(owner,Draft(f),f.Asset.Revision,Guid.NewGuid().ToString(),"Fictional wrong phrase"));
        var saved=facts.Review(owner,Draft(f),f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>facts.Review(owner,Draft(f,basis:26000),f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        var resources=new BusinessResources(f.Business.Store);Assert.Throws<ArgumentException>(()=>resources.SaveAsset(owner,saved with{TaxBasisCents=1}));
        Assert.Throws<ArgumentException>(()=>resources.SaveAsset(owner,saved with{FactHistory=[]}));
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>facts.Review(foreign.Owner,Draft(f),saved.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Single(resources.Assets(owner).Single().FactHistory);
    }

    [Theory]
    [InlineData("asset-facts")]
    [InlineData("audit")]
    public void InterruptedReviewRollsBackFactsHistoryAndAuditThenRetriesOnce(string stage)
    {
        using var f=new LedgerFixture();var facts=new AssetFacts(f.Business.Store);string request=Guid.NewGuid().ToString();long before=Convert.ToInt64(TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));
        facts.FailurePoint=p=>{if(p==stage)throw new IOException("Injected asset fact review failure");};
        Assert.Throws<IOException>(()=>facts.Review(f.Business.Owner,Draft(f),f.Asset.Revision,request,BusinessFixture.Phrase));
        var original=new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single();Assert.Null(original.CostCents);Assert.Empty(original.FactHistory);
        Assert.Equal(before,Convert.ToInt64(TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")));facts.FailurePoint=null;
        facts.Review(f.Business.Owner,Draft(f),f.Asset.Revision,request,BusinessFixture.Phrase);Assert.Single(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().FactHistory);
    }
}
