using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableDebtReportTablesTests
{
    private const string Private="Fictional-private-debt-report-evidence";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void LegacyMovementDoesNotAcquireCapturedLenderFromTheCurrentRegister()
    {
        using var f=new LedgerFixture();
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId,EvidenceId=Private});
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000));
        f.Business.Store.Write(f.Business.Owner,"Fictional legacy lender fixture",draft.Id,db=>PostingWriter.Save(db,f.Business.Owner,draft with{LoanLender=null},PostingRules.Build(draft,f.Accounts.Values.ToList()),null));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var tables=ReadableDebtReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.Equal(3,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var positions=tables.Single(t=>t.Name=="Report debt positions");var position=Assert.Single(positions.Rows);
        Assert.Equal(f.Business.Owner.OwnerId,Cell(positions,position,"OwnerLenderIdAtCapture"));
        Assert.Equal("10000",Cell(positions,position,"ClosingPrincipalCents"));
        var entries=tables.Single(t=>t.Name=="Report debt journal");Assert.NotEmpty(entries.Rows);
        Assert.All(entries.Rows,row=>
        {
            Assert.Equal("false",Cell(entries,row,"LenderFactsCaptured"));
            Assert.Equal("",Cell(entries,row,"CapturedLenderName"));Assert.Equal("",Cell(entries,row,"CapturedOwnerLenderId"));
        });
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void ActualRepaidZeroStaysDistinctFromRetainedUnknownPostedPrincipal()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000) with{Date=new(2027,2,1)});
        f.Post(f.Draft(FinancialKind.LoanPayment,10000) with{PrincipalCents=10000,Date=new(2027,2,2)});
        var resources=new BusinessResources(f.Business.Store);
        resources.SaveLoan(f.Business.Owner,f.Loan with{Name="Fictional current renamed lender",Archived=true,EvidenceId=Private});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableDebtReportTables.Build(source,new(),new(2027,1,1),new(2027,2,28));
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var positions=tables.Single(t=>t.Name=="Report debt positions");
        Assert.Equal("",Cell(positions,positions.Rows.Single(r=>r[0]==closed.Id),"ClosingPrincipalCents"));
        var current=positions.Rows.Single(r=>r[0]==source.Source.GenerationId);
        Assert.Equal("0",Cell(positions,current,"ClosingPrincipalCents"));Assert.Equal("true",Cell(positions,current,"ArchivedAtCapture"));
        Assert.Equal("10000",Cell(positions,current,"ProceedsCents"));Assert.Equal("10000",Cell(positions,current,"PrincipalPaidCents"));
        var entries=tables.Single(t=>t.Name=="Report debt journal");
        Assert.All(entries.Rows,row=>Assert.Equal(f.Loan.Name,Cell(entries,row,"CapturedLenderName")));
        Assert.DoesNotContain(entries.Rows,row=>row[0]==closed.Id);
        Assert.Equal(3,f.Book.Posted(f.Business.Owner).Count);
    }
}
