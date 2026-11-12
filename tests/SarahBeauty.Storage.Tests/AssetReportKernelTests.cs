using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class AssetReportKernelTests
{
    [Fact]
    public void CapturedAssetRollForwardsMatchHumanReportsAcrossDisposalAndReversal()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var disposal=f.Post(f.Draft(FinancialKind.AssetDisposal,25000) with{AssetId=f.Asset.Id,Date=new(2027,2,10),EvidenceId="Fictional scoped disposal evidence"});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=disposal.Id,Date=new(2027,3,10)});
        var expected=Enumerable.Range(1,3).Select(month=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month))).Assets!).ToArray();
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var resources=new BusinessResources(f.Business.Store);var asset=resources.Assets(f.Business.Owner).Single();
        resources.SaveAsset(f.Business.Owner,asset with{Name="Fictional later asset label"});f.Business.Owner.Lock();
        for(int month=1;month<=3;month++)
        {
            var actual=source.Read(db=>AssetSchedule.Read(db,source.Source.BusinessId,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month))));
            Assert.Equal(expected[month-1].Positions,actual.Positions);Assert.Equal(expected[month-1].Entries,actual.Entries);Assert.Equal(expected[month-1].Events,actual.Events);
        }
        Assert.Equal(20000,expected[0].Positions.Single().CarryingCents);Assert.True(expected[1].Positions.Single().DisposedAsOf);
        Assert.Equal(0,expected[1].Positions.Single().CarryingCents);Assert.False(expected[2].Positions.Single().DisposedAsOf);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RegisteredZeroDoesNotCreateBookValueAndForeignScopeHasNoAssetHistory()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();
        new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,f.Asset with{CostCents=0,TaxBasisCents=0});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        source.Read(db=>
        {
            var row=Assert.Single(AssetSchedule.Read(db,source.Source.BusinessId,f.Date,f.Date).Positions);
            Assert.Equal(0,row.CurrentFacts.CostCents);Assert.Equal(0,row.CurrentFacts.TaxBasisCents);
            Assert.Null(row.OpeningCostCents);Assert.Null(row.ClosingCostCents);Assert.Null(row.CarryingCents);
            Assert.Empty(AssetSchedule.Read(db,foreign.Owner.BusinessId,f.Date,f.Date).Positions);
            Assert.Throws<ArgumentException>(()=>AssetBookValue.Read(db,foreign.Owner.BusinessId,f.Asset.Id,f.Date));
            Assert.False(AssetDisposals.IsDisposed(db,foreign.Owner.BusinessId,f.Asset.Id,f.Date));
            return true;
        });
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
}
