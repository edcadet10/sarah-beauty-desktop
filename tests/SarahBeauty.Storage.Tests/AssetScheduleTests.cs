using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class AssetScheduleTests
{
    private static ReportSnapshot Report(LedgerFixture f,int month)=>new FinancialReports(f.Business.Store)
        .Load(f.Business.Owner,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)));

    [Fact]
    public void DisposalAndLaterReversalKeepPeriodRollForwardsAndHistoricalState()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var disposed=f.Post(f.Draft(FinancialKind.AssetDisposal,25000) with{AssetId=f.Asset.Id,Date=new(2027,2,10),EvidenceId="Fictional disposal evidence"});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=disposed.Id,Date=new(2027,3,10)});
        var jan=Assert.Single(Report(f,1).Assets!.Positions);
        Assert.Equal(0,jan.OpeningCostCents);Assert.Equal(30000,jan.CostIncreasesCents);
        Assert.Equal(10000,jan.DepreciationAddedCents);Assert.Equal(20000,jan.CarryingCents);Assert.False(jan.DisposedAsOf);
        var feb=Assert.Single(Report(f,2).Assets!.Positions);
        Assert.Equal(30000,feb.OpeningCostCents);Assert.Equal(10000,feb.OpeningDepreciationCents);
        Assert.Equal(30000,feb.CostReductionsCents);Assert.Equal(10000,feb.DepreciationClearedCents);
        Assert.Equal(0,feb.CarryingCents);Assert.True(feb.DisposedAsOf);
        var march=Report(f,3);var restored=Assert.Single(march.Assets!.Positions);
        Assert.Equal(30000,restored.CostIncreasesCents);Assert.Equal(10000,restored.DepreciationAddedCents);
        Assert.Equal(20000,restored.CarryingCents);Assert.False(restored.DisposedAsOf);
        Assert.Equal(march.TrialBalance.Where(t=>t.Account.Code is "1500" or "1590").Sum(t=>t.Ending),restored.CarryingCents);
        Assert.Contains(march.Assets.Entries,e=>e.AccountCode=="4010" && e.MovementId==disposed.Id && e.CreditCents==5000 && !e.IsBookValue);
    }

    [Fact]
    public void UnknownBookValueDoesNotBecomeRegisteredCostOrZeroAndZeroRetirementHasAnEvent()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        resources.SaveAsset(f.Business.Owner,f.Asset with{CostCents=40000,TaxBasisCents=35000});
        var before=Assert.Single(Report(f,1).Assets!.Positions);
        Assert.Null(before.CarryingCents);Assert.Null(before.OpeningCostCents);Assert.Equal(40000,before.CurrentFacts.CostCents);
        var source=f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.SupplierRefund,30000) with{OriginalId=source.Id,CounterAccountId=f.Accounts["1500"].Id,
            EvidenceId="Fictional complete basis return",AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Reduce)]});
        var disposal=f.Post(f.Draft(FinancialKind.AssetDisposal,0) with{AssetId=f.Asset.Id,EvidenceId="Fictional zero retirement"});
        var report=Report(f,1);var position=Assert.Single(report.Assets!.Positions);
        Assert.Equal(0,position.CarryingCents);Assert.True(position.DisposedAsOf);
        Assert.DoesNotContain(report.Assets.Entries,e=>e.MovementId==disposal.Id);
        Assert.Contains(report.Assets.Events,e=>e.MovementId==disposal.Id);
        var earlier=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2026,1,1),new(2026,12,31));
        Assert.Null(Assert.Single(earlier.Assets!.Positions).CarryingCents);
    }

    [Fact]
    public void MixedSupplierReturnKeepsAssetCostSeparateFromSuppliesAndCash()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var paid=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var refund=f.Post(f.Draft(FinancialKind.PurchaseRefund,25000) with{OriginalId=paid.Id,Date=new(2027,2,10),
            EvidenceId="Fictional selected equipment return",SupplierRefundPortions=[new(bill.Lines[1].Id,25000)],AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Retire)]});
        var january=Report(f,1);var asset=Assert.Single(january.Assets!.Positions);
        Assert.Equal(33000,asset.ClosingCostCents);Assert.Equal(23000,asset.CarryingCents);
        Assert.DoesNotContain(january.Assets.Entries,e=>e.AccountCode is "5000" or "1010");
        var february=Report(f,2);asset=Assert.Single(february.Assets!.Positions);
        Assert.True(asset.DisposedAsOf);Assert.Equal(33000,asset.CostReductionsCents);Assert.Equal(0,asset.CarryingCents);
        Assert.Contains(february.Assets.Events,e=>e.MovementId==refund.Id);
        Assert.Contains(february.Assets.Entries,e=>e.AccountCode=="4010" && e.CreditCents==2000);
    }

    [Fact]
    public void ArchivedAssetsAndCurrentFactsStayVisibleWithoutRewritingCapturedReports()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        var snapshot=Report(f,1);string json=JsonSerializer.Serialize(snapshot);
        var asset=new AssetFacts(f.Business.Store).Review(f.Business.Owner,new(f.Asset.Id,40000,35000,"Fictional fact review","Fictional evidence"),
            f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,asset with{Archived=true});
        var latest=Assert.Single(Report(f,1).Assets!.Positions);
        Assert.True(latest.CurrentFacts.Archived);Assert.Equal(40000,latest.CurrentFacts.CostCents);Assert.Equal(30000,latest.ClosingCostCents);
        Assert.Null(Assert.Single(JsonSerializer.Deserialize<ReportSnapshot>(json)!.Assets!.Positions).CurrentFacts.CostCents);
        var legacy=JsonNode.Parse(json)!.AsObject();legacy.Remove("Assets");
        Assert.Null(JsonSerializer.Deserialize<ReportSnapshot>(legacy.ToJsonString())!.Assets);
        Assert.Equal(1,snapshot.Assets!.Version);
    }

    [Fact]
    public void ConcurrentAcquisitionCannotMixScheduleAndTrialBalanceGenerations()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        var reports=new FinancialReports(f.Business.Store){AfterTrialRead=()=>f.Post(f.Draft(FinancialKind.AssetPurchase,10000))};
        var snapshot=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));
        Assert.Equal(30000,Assert.Single(snapshot.Assets!.Positions).ClosingCostCents);
        Assert.Equal(30000,snapshot.TrialBalance.Single(t=>t.Account.Code=="1500").Ending);
        Assert.Equal(40000,f.Balance("1500"));
        Assert.Single(snapshot.Assets.Events);
    }

    [Fact]
    public void OnePaymentWithTwoAssetsKeepsIndividualCostWithoutInventingCashAllocation()
    {
        using var f=new LedgerFixture();
        var second=new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,new Asset{Name="Fictional second equipment",BusinessUseFraction=1m});
        var source=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,40000));
        f.Business.Store.Write(f.Business.Owner,"Fictional multi-asset report probe",source.Id,db=>
        {
            List<JournalLine> lines=[new(f.Accounts["1500"].Id,30000,0,f.Asset.Id),new(f.Accounts["1500"].Id,10000,0,second.Id),new(f.Accounts["1010"].Id,0,40000)];
            var posted=PostingWriter.Save(db,f.Business.Owner,source,lines,null);
            JournalAssets.AfterPosting(db,f.Business.Owner,lines);return posted;
        });
        var snapshot=Report(f,1);
        Assert.Equal(2,snapshot.Assets!.Positions.Count);
        Assert.Equal(30000,snapshot.Assets.Positions.Single(a=>a.CurrentFacts.AssetId==f.Asset.Id).CarryingCents);
        Assert.Equal(10000,snapshot.Assets.Positions.Single(a=>a.CurrentFacts.AssetId==second.Id).CarryingCents);
        Assert.DoesNotContain(snapshot.Assets.Entries,e=>e.AccountCode=="1010");
        Assert.Equal(40000,snapshot.Assets.Positions.Sum(p=>p.CarryingCents));
    }
}
