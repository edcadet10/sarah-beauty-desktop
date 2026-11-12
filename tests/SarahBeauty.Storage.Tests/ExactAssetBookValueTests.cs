using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ExactAssetBookValueTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CancelledAcquisitionCostsRemainExactRegardlessOfJournalIndexOrder(bool creditsFirst)
    {
        using var f=new LedgerFixture();var source=f.Post(f.Draft(FinancialKind.AssetPurchase,long.MaxValue));f.Post(f.Draft(FinancialKind.SupplierRefund,long.MaxValue) with{OriginalId=source.Id,CounterAccountId=source.Source.CounterAccountId});f.Post(f.Draft(FinancialKind.AssetPurchase,long.MaxValue));
        f.Business.Files.Execute("DROP INDEX JournalLines_Asset; DROP INDEX JournalLines_Account; CREATE INDEX ExactAssetProbe ON JournalLines(AssetId,AccountId,"+(creditsFirst?"CreditCents":"DebitCents")+" DESC,MovementId);");
        var value=f.Business.Store.Read(db=>AssetBookValue.Read(db,f.Business.Owner,f.Asset.Id,f.Date));Assert.Equal(long.MaxValue,value.Cost);Assert.Equal(long.MaxValue,value.Carrying);Assert.Equal(0,value.Depreciation);
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.AssetDisposal,long.MaxValue) with{AssetId=f.Asset.Id,EvidenceId="Fictional reviewed asset disposal"});long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));var preview=f.Book.Preview(f.Business.Owner,draft);Assert.Equal(2,preview.Count);Assert.Contains(preview,v=>v.DebitCents==long.MaxValue);Assert.Contains(preview,v=>v.CreditCents==long.MaxValue);Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    }
    [Fact]
    public void ATrueOutOfRangeAssetValueGivesAControlledErrorWithoutPosting()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.AssetPurchase,long.MaxValue));f.Post(f.Draft(FinancialKind.AssetPurchase,1));var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.AssetDisposal,1) with{AssetId=f.Asset.Id,EvidenceId="Fictional reviewed asset disposal"});long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));var error=Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,draft));Assert.Contains("supported cent range",error.Message,StringComparison.Ordinal);Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
}
