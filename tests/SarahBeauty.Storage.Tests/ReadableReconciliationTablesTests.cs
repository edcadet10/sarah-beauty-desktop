using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableReconciliationTablesTests
{
    private const string Private="Fictional-private-reconciliation-review-evidence";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void ReopenedCompletionKeepsFrozenBalancesOutstandingAndProvenanceAfterLaterCashChanges()
    {
        using var f=new LedgerFixture();
        ReconciliationPreviewTests.Zero(f);
        string statement=ReconciliationPreviewTests.NoActivity(f);
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var book=new Reconciliations(f.Business.Store);
        var saved=ReconciliationCloseTests.Complete(f,book,new(statement,Private){Outstanding=[new(cash.Id,10000,Private,Private)]});
        book.Reopen(f.Business.Owner,saved.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Expense,2500));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableReconciliationTables.Build(source,new());
        Assert.Equal(13,tables.Length);
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var approvals=tables.Single(t=>t.Name=="Reconciliation approvals");
        var approval=Assert.Single(approvals.Rows);
        Assert.Equal("true",Cell(approvals,approval,"Reopened"));
        Assert.Equal(saved.SnapshotHash,Cell(approvals,approval,"SnapshotSha256"));
        var balances=tables.Single(t=>t.Name=="Reconciliation frozen balances");
        var row=Assert.Single(balances.Rows);
        Assert.Equal("10000",Cell(balances,row,"LedgerClosingCents"));
        Assert.Equal("0",Cell(balances,row,"StatementClosingCents"));
        Assert.Equal("10000",Cell(balances,row,"AdjustedStatementCents"));
        Assert.Equal("0",Cell(balances,row,"DifferenceCents"));
        Assert.Equal("true",Cell(balances,row,"ReadyToComplete"));
        Assert.Equal(saved.Snapshot.Sources.Watermark.ToString(),Cell(balances,row,"ObservedWatermark"));
        var outstanding=tables.Single(t=>t.Name=="Reconciliation outstanding cash");
        Assert.Equal(cash.Id,Cell(outstanding,Assert.Single(outstanding.Rows),"JournalLineId"));
        Assert.Single(tables.Single(t=>t.Name=="Reconciliation reopens").Rows);
        Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public void ActualBankErrorAndCorrectionRetainCoverageClaimsAndZeroRemainingWithoutJournalMoney()
    {
        using var f=new LedgerFixture();
        ReconciliationPreviewTests.Zero(f);
        var lines=BankMatchingTests.Statement(f,2500,-2500);
        string statement=new StatementBook(f.Business.Store).List(f.Business.Owner).Single().Id;
        var error=new StatementError(Guid.NewGuid().ToString(),lines[0].Id,2500,Private,Private);
        var saved=ReconciliationCloseTests.Complete(f,new(f.Business.Store),new(statement,Private)
        {
            StatementErrors=[error],BankCorrections=[new(error.Id,lines[1].Id,2500,Private,Private)]
        });
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableReconciliationTables.Build(source,new());
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var errors=tables.Single(t=>t.Name=="Reconciliation statement errors");
        Assert.Equal(error.Id,Assert.Single(errors.Rows)[0]);
        var resolutions=tables.Single(t=>t.Name=="Reconciliation error resolutions");
        Assert.Equal(error.Id,Cell(resolutions,Assert.Single(resolutions.Rows),"ErrorId"));
        var carried=tables.Single(t=>t.Name=="Reconciliation carried errors");
        var row=Assert.Single(carried.Rows);
        Assert.Equal("Remaining",Cell(carried,row,"Phase"));
        Assert.Equal("0",Cell(carried,row,"RemainingCents"));
        Assert.Equal(2,tables.Single(t=>t.Name=="Reconciliation bank error claims").Rows.Count);
        Assert.Equal(2,tables.Single(t=>t.Name=="Reconciliation bank coverage").Rows.Count);
        Assert.Empty(tables.Single(t=>t.Name=="Reconciliation recorded warnings").Rows);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(saved.Snapshot.Sources.Watermark+1,source.Source.Watermark);
    }

    [Fact]
    public void AHashValidSnapshotWithChangedRelationalIdentityIsRejected()
    {
        using var f=new LedgerFixture();
        ReconciliationPreviewTests.Zero(f);
        string statement=ReconciliationPreviewTests.NoActivity(f);
        ReconciliationCloseTests.Complete(f,new(f.Business.Store),new(statement,Private));
        string raw=(string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SnapshotJson FROM Reconciliations;")!;
        var changed=JsonNode.Parse(raw)!;
        changed["Sources"]!["Statement"]!["Id"]=Guid.NewGuid().ToString();
        string json=changed.ToJsonString();
        f.Business.Store.Transaction(db=>
        {
            db.Execute("DROP TRIGGER Reconciliations_NoUpdate;");
            return db.Execute("UPDATE Reconciliations SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",ReconciliationQueries.Hash(json)));
        });
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        Assert.Throws<InvalidDataException>(()=>ReadableReconciliationTables.Build(source,new()));
    }
}
