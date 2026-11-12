using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableTaxReportTablesTests
{
    private const string Private="Fictional-private-tax-report-evidence-reason";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private static TaxPeriod Period(LedgerFixture f)=>new TaxPeriodBook(f.Business.Store).Save(f.Business.Owner,new(){Name="Fictional readable return",Jurisdiction="Fictional jurisdiction",From=new(2027,1,1),Through=new(2027,1,31)});
    private static void FileZero(LedgerFixture f,TaxPeriod period)=>new TaxPeriodBook(f.Business.Store).RecordStatus(f.Business.Owner,period.Id,new(Guid.NewGuid().ToString(),period.Revision,"Filed",new(2027,2,15),0,"Fictional filed return reference",Private,Private),BusinessFixture.Phrase);

    [Fact]
    public void PartialTaxRecoveryAndFiledZeroKeepExactAmountsWithoutPrivateReviewInputs()
    {
        using var f=new LedgerFixture();
        var payment=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        var refund=f.Post(f.Draft(FinancialKind.Refund,11000) with{OriginalId=payment.Id,TaxCents=1000,TaxRefundAllocation=new(0,1000,0),EvidenceId=Private});
        FinancialDraft Recover()=>f.Draft(FinancialKind.TaxRecovery,500) with{OriginalId=refund.Id,EvidenceId=Private};
        f.Post(Recover());FileZero(f,Period(f));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(Recover());f.Business.Owner.Lock();
        var tables=ReadableTaxReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.Equal(4,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var tax=tables.Single(t=>t.Name=="Report tax amounts");var row=Assert.Single(tax.Rows);
        Assert.Equal("1000",Cell(tax,row,"CollectedCents"));Assert.Equal("1000",Cell(tax,row,"RefundedCents"));
        Assert.Equal("1000",Cell(tax,row,"RemittedCents"));Assert.Equal("0",Cell(tax,row,"LiabilityCents"));
        Assert.Equal("500",Cell(tax,row,"RecoverableCents"));Assert.Equal("500",Cell(tax,row,"RecoveredCashCents"));
        var history=tables.Single(t=>t.Name=="Report tax filing decisions");var filed=Assert.Single(history.Rows);
        Assert.Equal("0",Cell(history,filed,"ReportedTaxCents"));Assert.Equal("Alex",Cell(history,filed,"OwnerNameAtCapture"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedFilingHistoryStaysOriginalAfterReopenAndLaterFiling()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        var period=Period(f);var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);FileZero(f,period);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableTaxReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var filing=tables.Single(t=>t.Name=="Report tax filing periods");
        Assert.Equal("Unreviewed",Cell(filing,filing.Rows.Single(r=>r[0]==closed.Id),"StatusAtCapture"));
        Assert.Equal("Filed",Cell(filing,filing.Rows.Single(r=>r[0]==source.Source.GenerationId),"StatusAtCapture"));
        Assert.All(filing.Rows,r=>Assert.Equal("",Cell(filing,r,"DueDate")));
        Assert.DoesNotContain(tables.Single(t=>t.Name=="Report tax filing decisions").Rows,r=>r[0]==closed.Id);
        Assert.Equal(2,tables.Single(t=>t.Name=="Report tax amounts").Rows.Count);
    }

    [Fact]
    public void MissingLegacyFilingSnapshotIsMarkedUnretainedWithoutInventedRows()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        var closed=BookPeriodTests.Close(f,new(f.Business.Store));
        var node=JsonNode.Parse((string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SnapshotJson FROM BookPeriodCloses;")!)!;
        node["Reports"]!.AsObject().Remove("TaxFilings");string json=node.ToJsonString();
        f.Business.Store.Transaction(db=>
        {
            db.Execute("DROP TRIGGER BookPeriodCloses_NoUpdate;");
            return db.Execute("UPDATE BookPeriodCloses SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",BookPeriodQueries.Hash(json)));
        });
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableTaxReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        var coverage=tables.Single(t=>t.Name=="Tax report filing coverage");
        Assert.Equal("Not retained",Cell(coverage,coverage.Rows.Single(r=>r[0]==closed.Id),"FilingFacts"));
        Assert.DoesNotContain(tables.Single(t=>t.Name=="Report tax filing periods").Rows,r=>r[0]==closed.Id);
    }
}
