using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableAssetReportTablesTests
{
    private const string Private="Fictional-private-asset-report-evidence";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void UnknownBookValuesStayBlankAndActualZeroRetirementKeepsAnEventWithoutInventedJournal()
    {
        using var f=new LedgerFixture();
        new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,f.Asset with{CostCents=0,TaxBasisCents=0,EvidenceId=Private});
        using var unknown=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var before=ReadableAssetReportTables.Build(unknown,new(),new(2027,1,1),new(2027,1,31));
        var positions=before.Single(t=>t.Name=="Report asset positions");var blank=Assert.Single(positions.Rows);
        Assert.Equal("0",Cell(positions,blank,"RegisteredCostCentsAtCapture"));
        Assert.Equal("",Cell(positions,blank,"OpeningCostCents"));Assert.Equal("",Cell(positions,blank,"CarryingCents"));
        var purchase=f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.SupplierRefund,30000) with{OriginalId=purchase.Id,CounterAccountId=f.Accounts["1500"].Id,EvidenceId=Private,AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Reduce)]});
        var retirement=f.Post(f.Draft(FinancialKind.AssetDisposal,0) with{AssetId=f.Asset.Id,EvidenceId=Private});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var tables=ReadableAssetReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.Equal(4,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        positions=tables.Single(t=>t.Name=="Report asset positions");var zero=Assert.Single(positions.Rows);
        Assert.Equal("0",Cell(positions,zero,"CarryingCents"));Assert.Equal("true",Cell(positions,zero,"DisposedAsOf"));
        var entries=tables.Single(t=>t.Name=="Report asset journal");
        Assert.DoesNotContain(entries.Rows,r=>Cell(entries,r,"MovementId")==retirement.Id);
        var events=tables.Single(t=>t.Name=="Report asset events");
        Assert.Contains(events.Rows,r=>Cell(events,r,"MovementId")==retirement.Id);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedRegisterFactsStayOriginalBesideLaterReviewedRegisterValues()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var asset=new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single();
        new AssetFacts(f.Business.Store).Review(f.Business.Owner,new(asset.Id,50000,40000,Private,Private),asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableAssetReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var positions=tables.Single(t=>t.Name=="Report asset positions");
        Assert.Equal("",Cell(positions,positions.Rows.Single(r=>r[0]==closed.Id),"RegisteredCostCentsAtCapture"));
        Assert.Equal("50000",Cell(positions,positions.Rows.Single(r=>r[0]==source.Source.GenerationId),"RegisteredCostCentsAtCapture"));
        Assert.All(positions.Rows,r=>Assert.Equal("",Cell(positions,r,"CarryingCents")));
        Assert.All(positions.Rows,r=>Assert.Equal("true",Cell(positions,r,"ArchivedAtCapture")));
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }
}
