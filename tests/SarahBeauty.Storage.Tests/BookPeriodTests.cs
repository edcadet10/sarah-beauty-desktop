using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BookPeriodTests
{
    private static BookPeriodDraft Draft(int month=1)=>new(new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)),"Fictional reviewed book closing","Fictional closing evidence");

    private static void Ready(LedgerFixture f)
    {
        new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,f.Asset with{Archived=true});
        f.Post(f.Draft(FinancialKind.Receipt,10000));
        var openings=new OpeningBalances(f.Business.Store);
        foreach(var account in f.Accounts.Values.Where(a=>a.CashKind!="None"))
            openings.ConfirmZero(f.Business.Owner,account.Id,new(2027,1,1),"Fictional confirmed opening",BusinessFixture.Phrase);
        ReconcileMonth(f,1);
    }

    private static void ReconcileMonth(LedgerFixture f,int month)
    {
        var draft=Draft(month);var statements=new StatementBook(f.Business.Store);var recon=new Reconciliations(f.Business.Store);
        foreach(var account in f.Accounts.Values.Where(a=>a.CashKind!="None"))
        {
            long closing=account.Code=="1010"?10000:0;bool deposit=month==1 && closing>0;
            var source=deposit?StatementCsv.Parse($"Date,Description,Amount\n2027-01-10,Fictional confirmed deposit,{Money.Input(closing)}",new()):new StatementSource([],"","{}");
            var statement=new StatementDraft(account.Id,draft.From,draft.Through,deposit?0:closing,closing,source,"Fictional complete month statement");
            var saved=statements.Import(f.Business.Owner,statement,statements.Preview(f.Business.Owner,statement).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
            if(deposit)
            {
                var bank=statements.SourceRows(f.Business.Owner,saved.Id).Single().Line;
                var cash=new BankMatching(f.Business.Store).CashLines(f.Business.Owner,account.Id).Single();
                BankMatchingTests.Match(f,new(f.Business.Store),cash.Date,new MatchPortion(bank.Id,cash.Id,closing));
            }
            ReconciliationCloseTests.Complete(f,recon,new(saved.Id,"Fictional period-end account review"));
        }
    }

    private static ClosedBookPeriod Close(LedgerFixture f,BookPeriods book,BookPeriodDraft? draft=null)
    {
        draft??=Draft();var preview=book.Preview(f.Business.Owner,draft);
        Assert.Empty(preview.BlockingIssues);
        return book.Close(f.Business.Owner,draft,preview.Reports.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }

    [Fact]
    public void ClosePinsAllCurrentFinancialReportsAndExactRetriesDoNotDuplicateOrRelock()
    {
        using var f=new LedgerFixture();Ready(f);var book=new BookPeriods(f.Business.Store);var draft=Draft();var preview=book.Preview(f.Business.Owner,draft);
        string request=Guid.NewGuid().ToString();var saved=book.Close(f.Business.Owner,draft,preview.Reports.Watermark,request,BusinessFixture.Phrase);
        Assert.Equal(new DateOnly(2027,1,31),book.ClosedThrough(f.Business.Owner));
        Assert.Equal(10000,saved.Snapshot.Reports.ProfitLoss.IncomeCents);
        Assert.Equal(1,saved.Snapshot.Reports.Assets!.Version);
        Assert.Equal(1,saved.Snapshot.Reports.Debts!.Version);
        Assert.Equal(1,saved.Snapshot.Reports.Owners!.Version);
        Assert.True(Assert.Single(saved.Snapshot.Reports.Assets.Positions).CurrentFacts.Archived);
        Assert.Equal(3,saved.Snapshot.Reconciliations.Length);
        Assert.Equal(f.Business.Owner.OwnerId,saved.ApprovedBy);
        Assert.Equal(saved.Id,book.Close(f.Business.Owner,draft,preview.Reports.Watermark,request.ToUpperInvariant(),BusinessFixture.Phrase).Id);
        Assert.Throws<ArgumentException>(()=>book.Close(f.Business.Owner,draft with{Reason="Changed reason"},preview.Reports.Watermark,request,BusinessFixture.Phrase));
        Assert.Single(book.History(f.Business.Owner));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Close book period';"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM BookPeriodCloses;"));
        book.Reopen(f.Business.Owner,saved.Id,"Fictional revised review",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.True(book.Close(f.Business.Owner,draft,preview.Reports.Watermark,request,BusinessFixture.Phrase).Reopened);
        Assert.Null(book.ClosedThrough(f.Business.Owner));
        Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        new AssetFacts(f.Business.Store).Review(f.Business.Owner,new(f.Asset.Id,50000,40000,"Fictional later register review","Fictional later evidence"),
            new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Null(Assert.Single(book.Open(f.Business.Owner,saved.Id).Snapshot.Reports.Assets!.Positions).CurrentFacts.CostCents);
    }

    [Fact]
    public void UnknownOpeningsMissingReconciliationsAndSkippedInitialHistoryBlockClosing()
    {
        using var f=new LedgerFixture();var book=new BookPeriods(f.Business.Store);var preview=book.Preview(f.Business.Owner,Draft());
        Assert.True(preview.Reports.BalanceSheet.Balances);Assert.False(preview.ReadyToClose);
        Assert.Contains(preview.BlockingIssues,s=>s.Contains("opening"));
        Assert.Contains(preview.BlockingIssues,s=>s.Contains("reconciliation"));
        Assert.Throws<ArgumentException>(()=>book.Close(f.Business.Owner,Draft(),preview.Reports.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Ready(f);
        Assert.False(book.Preview(f.Business.Owner,Draft() with{From=new(2027,1,2)}).ReadyToClose);
        Assert.Empty(book.History(f.Business.Owner));
    }

    [Fact]
    public void NewStatementAccountsCannotBypassClosedBookDates()
    {
        using var f=new LedgerFixture();Ready(f);var periods=new BookPeriods(f.Business.Store);Close(f,periods);
        var chart=new ChartOfAccounts(f.Business.Store);
        var account=chart.Save(f.Business.Owner,new("","1030","Fictional additional bank","Asset","Bank",true,false));
        var source=StatementCsv.Parse("Date,Description,Amount\n2027-01-15,Fictional discovered old bank item,1.00",new());
        var draft=new StatementDraft(account.Id,new(2027,1,1),new(2027,2,28),0,100,source);
        var statements=new StatementBook(f.Business.Store);var preview=statements.Preview(f.Business.Owner,draft);
        Assert.Contains("closed period",Assert.Throws<ArgumentException>(()=>statements.Import(f.Business.Owner,draft,preview.Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase)).Message);
        Assert.Empty(statements.BankLines(f.Business.Owner,account.Id));
    }

    [Fact]
    public void StaleReviewsAndWrongOwnerConfirmationDoNotCloseOrDiscardOtherDrafts()
    {
        using var f=new LedgerFixture();Ready(f);var book=new BookPeriods(f.Business.Store);var preview=book.Preview(f.Business.Owner,Draft());
        Assert.Throws<UnauthorizedAccessException>(()=>book.Close(f.Business.Owner,Draft(),preview.Reports.Watermark,Guid.NewGuid().ToString(),"Fictional wrong passphrase"));
        new CustomerBook(f.Business.Store).Save(f.Business.Owner,new(){FullName="Fictional later customer edit"});
        Assert.Throws<ArgumentException>(()=>book.Close(f.Business.Owner,Draft(),preview.Reports.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Null(book.ClosedThrough(f.Business.Owner));Assert.Empty(book.History(f.Business.Owner));
    }

    [Fact]
    public void ReopeningRequiresLatestPeriodFirstAndRestoresThePreviousLock()
    {
        using var f=new LedgerFixture();Ready(f);var book=new BookPeriods(f.Business.Store);var january=Close(f,book);
        ReconcileMonth(f,2);var february=Close(f,book,Draft(2));
        Assert.Throws<ArgumentException>(()=>book.Reopen(f.Business.Owner,january.Id,"Fictional earlier change",Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        string request=Guid.NewGuid().ToString();var reopened=book.Reopen(f.Business.Owner,february.Id,"Fictional later change",request,BusinessFixture.Phrase);
        Assert.Equal(new DateOnly(2027,1,31),book.ClosedThrough(f.Business.Owner));
        Assert.Equal(reopened.Id,book.Reopen(f.Business.Owner,february.Id,"Fictional later change",request,BusinessFixture.Phrase).Id);
        Assert.Equal("Fictional later change",book.ReopenHistory(f.Business.Owner,february.Id).Single().Reason);
        Assert.False(book.Preview(f.Business.Owner,Draft(2) with{From=new(2027,2,2)}).ReadyToClose);
        book.Reopen(f.Business.Owner,january.Id,"Fictional earlier change",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Null(book.ClosedThrough(f.Business.Owner));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM BookPeriodReopens;"));
    }

    [Fact]
    public void RevisedClosingRetainsOriginalReportsAndRequiresSeparateCashReconciliationReopening()
    {
        using var f=new LedgerFixture();Ready(f);var book=new BookPeriods(f.Business.Store);var original=Close(f,book);
        var expense=f.Draft(FinancialKind.Expense,100) with{Date=new(2027,1,15)};
        Assert.Contains("closed period",Assert.Throws<ArgumentException>(()=>f.Post(expense)).Message);
        book.Reopen(f.Business.Owner,original.Id,"Fictional omitted check found",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Contains("reconciliation",Assert.Throws<ArgumentException>(()=>f.Post(expense)).Message);
        var bank=original.Snapshot.Reconciliations.Single(r=>r.AccountId==f.Accounts["1010"].Id);var recon=new Reconciliations(f.Business.Store);
        recon.Reopen(f.Business.Owner,bank.Id,"Fictional omitted check review",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var posted=f.Post(expense);var cash=BankMatchingTests.Cash(f,posted);
        ReconciliationCloseTests.Complete(f,recon,new(bank.StatementId,"Fictional revised bank review")
            {Outstanding=[new(cash.Id,100,"Fictional actual check still outstanding","Fictional check evidence")]});
        var revised=Close(f,book);
        Assert.Equal(9900,revised.Snapshot.Reports.ProfitLoss.ResultCents);
        Assert.Equal(10000,book.Open(f.Business.Owner,original.Id).Snapshot.Reports.ProfitLoss.ResultCents);
        Assert.Equal(original.SnapshotHash,book.Open(f.Business.Owner,original.Id).SnapshotHash);
        Assert.NotEqual(original.Id,revised.Id);Assert.Equal(2,book.History(f.Business.Owner).Count);
    }

    [Theory]
    [InlineData("period-snapshot")]
    [InlineData("period-lock")]
    [InlineData("audit")]
    public void InterruptedCloseRollsBackReportsLockAndAuditThenRetriesOnce(string stage)
    {
        using var f=new LedgerFixture();Ready(f);var book=new BookPeriods(f.Business.Store);var preview=book.Preview(f.Business.Owner,Draft());string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected period closing failure");};
        Assert.Throws<IOException>(()=>book.Close(f.Business.Owner,Draft(),preview.Reports.Watermark,request,BusinessFixture.Phrase));
        Assert.Null(book.ClosedThrough(f.Business.Owner));Assert.Empty(book.History(f.Business.Owner));
        book.FailurePoint=null;
        book.Close(f.Business.Owner,Draft(),preview.Reports.Watermark,request,BusinessFixture.Phrase);
        Assert.Single(book.History(f.Business.Owner));
    }

    [Fact]
    public void FailedReopeningLeavesTheLockAndOriginalReportsIntact()
    {
        using var f=new LedgerFixture();Ready(f);var book=new BookPeriods(f.Business.Store);var saved=Close(f,book);
        book.FailurePoint=p=>{if(p=="period-reopen")throw new IOException("Injected period reopening failure");};
        Assert.Throws<IOException>(()=>book.Reopen(f.Business.Owner,saved.Id,"Fictional retry review",Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Equal(Draft().Through,book.ClosedThrough(f.Business.Owner));
        Assert.False(book.Open(f.Business.Owner,saved.Id).Reopened);
        Assert.Empty(book.ReopenHistory(f.Business.Owner,saved.Id));
    }

    [Fact]
    public void VersionTwentyFiveUpgradeRetainsReconciliationsAndReportHashesDetectTampering()
    {
        using var f=new LedgerFixture();Ready(f);
        f.Business.Files.PrepareBankingVersion(25);
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var book=new BookPeriods(f.Business.Store);var saved=Close(f,book);
        Assert.Equal(3,saved.Snapshot.Reconciliations.Length);
        f.Business.Files.Execute("DROP TRIGGER BookPeriodCloses_NoUpdate; UPDATE BookPeriodCloses SET SnapshotJson='{}';");
        Assert.Throws<InvalidDataException>(()=>book.Open(f.Business.Owner,saved.Id));
    }
}
