using System.Text.Json;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class TaxFilingReportTests
{
    private static TaxPeriod Period(string name="Fictional January tax",string jurisdiction="Fictional jurisdiction")=>new(){Name=name,Jurisdiction=jurisdiction,From=new(2027,1,1),Through=new(2027,1,31)};
    private static ReportSnapshot Report(LedgerFixture f,DateOnly? from=null,DateOnly? through=null)=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,from??new(2027,1,1),through??new(2027,1,31));
    private static TaxFilingDecision Decision(TaxPeriod period)=>new(Guid.NewGuid().ToString(),period.Revision,"Filed",new(2027,2,15),950,"Fictional filing reference","Fictional filing evidence","Fictional actual filing review");

    [Fact]
    public void FilingAtCaptureStaysSeparateFromHistoricalMoneyAndRetainedReports()
    {
        using var f=new LedgerFixture();var book=new TaxPeriodBook(f.Business.Store);var period=book.Save(f.Business.Owner,Period());
        f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        Assert.Equal("Unreviewed",Assert.Single(Report(f).TaxFilings!.Periods).StatusAtCapture);
        var filed=book.RecordStatus(f.Business.Owner,period.Id,Decision(period),BusinessFixture.Phrase);var report=Report(f);string json=JsonSerializer.Serialize(report);
        var captured=Assert.Single(report.TaxFilings!.Periods);var history=Assert.Single(captured.History);
        Assert.Equal("Filed",captured.StatusAtCapture);Assert.Equal(new DateOnly(2027,2,15),history.Decision.FilingDate);Assert.Equal(950,history.Decision.ReportedTaxCents);
        Assert.Equal("Alex",history.OwnerNameAtCapture);Assert.Equal(1000,report.Tax.CollectedCents);Assert.Equal(1000,report.Tax.RemittedCents);Assert.Equal(0,report.Tax.LiabilityCents);
        book.RecordStatus(f.Business.Owner,period.Id,Decision(filed) with{Status="Amended",FilingDate=new(2027,2,20),ReportedTaxCents=-100},BusinessFixture.Phrase);
        Assert.Equal("Filed",Assert.Single(JsonSerializer.Deserialize<ReportSnapshot>(json)!.TaxFilings!.Periods).StatusAtCapture);
        Assert.Equal("Amended",Assert.Single(Report(f).TaxFilings!.Periods).StatusAtCapture);Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
        var legacy=JsonSerializer.SerializeToNode(report)!.AsObject();legacy.Remove("TaxFilings");Assert.Null(JsonSerializer.Deserialize<ReportSnapshot>(legacy.ToJsonString())!.TaxFilings);
    }

    [Fact]
    public void OverlappingQuarterAndArchivedPeriodsRetainTheirJurisdictionsAndUnknownDueDates()
    {
        using var f=new LedgerFixture();var book=new TaxPeriodBook(f.Business.Store);
        var january=book.Save(f.Business.Owner,Period());book.Save(f.Business.Owner,january with{Archived=true});
        book.Save(f.Business.Owner,Period("Fictional February") with{From=new(2027,2,1),Through=new(2027,2,28)});
        book.Save(f.Business.Owner,Period("Fictional quarter","Fictional other jurisdiction") with{Through=new(2027,3,31)});
        var jan=Report(f).TaxFilings!;Assert.Equal(2,jan.Periods.Count);Assert.True(jan.Periods.Single(p=>p.Id==january.Id).Archived);Assert.All(jan.Periods,p=>Assert.Null(p.DueDate));
        var boundary=Report(f,new(2027,2,1),new(2027,2,1)).TaxFilings!;Assert.Equal(2,boundary.Periods.Count);Assert.DoesNotContain(boundary.Periods,p=>p.Id==january.Id);
        Assert.Empty(Report(f,new(2026,12,1),new(2026,12,31)).TaxFilings!.Periods);Assert.Equal(1,jan.Version);
    }

    [Fact]
    public void ConcurrentFilingDecisionCannotMixStatusHistoryAndReportWatermark()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");var book=new TaxPeriodBook(f.Business.Store);var period=book.Save(f.Business.Owner,Period());
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var reports=new FinancialReports(f.Business.Store){AfterTrialRead=()=>book.RecordStatus(f.Business.Owner,period.Id,Decision(period),BusinessFixture.Phrase)};
        var snapshot=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));var captured=Assert.Single(snapshot.TaxFilings!.Periods);
        Assert.Equal(watermark,snapshot.Watermark);Assert.Equal("Unreviewed",captured.StatusAtCapture);Assert.Empty(captured.History);
        Assert.Equal("Filed",Assert.Single(Report(f).TaxFilings!.Periods).StatusAtCapture);Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
}
