using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ReconciliationCloseTests
{
    private static (ReconciliationDraft Draft,CashMatchLine Cash,BankLine Bank) Ready(LedgerFixture f)
    {
        ReconciliationPreviewTests.Zero(f);
        var bank=BankMatchingTests.Statement(f,10000).Single();
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        BankMatchingTests.Match(f,new BankMatching(f.Business.Store),f.Date,new MatchPortion(bank.Id,cash.Id,10000));
        string statement=new StatementBook(f.Business.Store).List(f.Business.Owner).Single().Id;
        return(new(statement,"Fictional complete reconciliation review"),cash,bank);
    }

    internal static CompletedReconciliation Complete(LedgerFixture f,Reconciliations book,ReconciliationDraft draft)
    {
        var preview=book.Preview(f.Business.Owner,draft);
        return book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }

    [Fact]
    public void CompletionPinsVerifiedSnapshotAndExactRetriesReturnTheOriginalApproval()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);
        var book=new Reconciliations(f.Business.Store);
        var preview=book.Preview(f.Business.Owner,ready.Draft);
        string request=Guid.NewGuid().ToString();
        var saved=book.Complete(f.Business.Owner,ready.Draft,preview.Sources.Watermark,request,BusinessFixture.Phrase);
        var retry=book.Complete(f.Business.Owner,ready.Draft,preview.Sources.Watermark,request.ToUpperInvariant(),BusinessFixture.Phrase);
        Assert.Equal(saved.Id,retry.Id);
        Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        Assert.Equal(f.Business.Owner.OwnerId,saved.ApprovedBy);
        Assert.Equal(10000,saved.Snapshot.Sources.LedgerClosingCents);
        Assert.Single(book.History(f.Business.Owner));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM ReconciliationBankCoverage;"));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Complete reconciliation';"));
        Assert.Throws<ArgumentException>(()=>book.Complete(f.Business.Owner,ready.Draft with{Reason="Changed retry"},preview.Sources.Watermark,request,BusinessFixture.Phrase));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE Reconciliations SET SnapshotJson='{}';"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM ReconciliationBankCoverage;"));
        Assert.Equal(10000,f.Balance("1010"));
    }

    [Fact]
    public void AZeroDifferenceWithUnexplainedItemsCannotBeCompleted()
    {
        using var f=new LedgerFixture();
        ReconciliationPreviewTests.Zero(f);
        BankMatchingTests.Statement(f,2500,-2500);
        string statement=new StatementBook(f.Business.Store).List(f.Business.Owner).Single().Id;
        var book=new Reconciliations(f.Business.Store);
        var draft=new ReconciliationDraft(statement,"Fictional unexplained net-zero attempt");
        var preview=book.Preview(f.Business.Owner,draft);
        Assert.Equal(0,preview.DifferenceCents);
        Assert.Throws<ArgumentException>(()=>book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));
    }

    [Fact]
    public void StaleReviewAndIncorrectOwnerConfirmationCannotComplete()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);
        var book=new Reconciliations(f.Business.Store);
        var preview=book.Preview(f.Business.Owner,ready.Draft);
        Assert.Throws<UnauthorizedAccessException>(()=>book.Complete(f.Business.Owner,ready.Draft,preview.Sources.Watermark,Guid.NewGuid().ToString(),"Fictional wrong passphrase"));
        new CustomerBook(f.Business.Store).Save(f.Business.Owner,new(){FullName="Fictional later edit"});
        Assert.Throws<ArgumentException>(()=>book.Complete(f.Business.Owner,ready.Draft,preview.Sources.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));
    }

    [Fact]
    public void CompletedDatesBlockCashMatchingOpeningsAndNewHistoricalBankLinesUntilReopened()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);
        var book=new Reconciliations(f.Business.Store);
        var saved=Complete(f,book,ready.Draft);
        var expense=f.Draft(FinancialKind.Expense,100) with{Date=f.Date.AddDays(1)};
        Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,expense));
        Assert.Throws<ArgumentException>(()=>f.Post(expense));
        var matching=new BankMatching(f.Business.Store);
        var original=matching.History(f.Business.Owner).Single();
        Assert.Throws<ArgumentException>(()=>BankMatchingTests.Match(f,matching,f.Date,new MatchPortion(ready.Bank.Id,ready.Cash.Id,1,original.Id)));
        var opening=new OpeningBalances(f.Business.Store).List(f.Business.Owner).Single();
        Assert.Throws<ArgumentException>(()=>new OpeningBalances(f.Business.Store).RevokeZero(f.Business.Owner,opening.Id,"Fictional opening revision",BusinessFixture.Phrase));
        var source=StatementCsv.Parse("Date,Description,Amount\n2027-01-15,Fictional newly discovered old item,1",new());
        var old=new StatementDraft(ready.Bank.AccountId,new(2027,1,1),new(2027,2,28),0,100,source);
        var statements=new StatementBook(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>statements.Import(f.Business.Owner,old,statements.Preview(f.Business.Owner,old).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        book.Reopen(f.Business.Owner,saved.Id,"Fictional documented correction",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var correction=f.Post(expense);
        var revised=ready.Draft with{Reason="Fictional revised reconciliation",Outstanding=[new(BankMatchingTests.Cash(f,correction).Id,100,"Actual check still outstanding","Fictional check evidence")]};
        var newer=Complete(f,book,revised);
        Assert.NotEqual(saved.Id,newer.Id);
        Assert.True(book.Open(f.Business.Owner,saved.Id).Reopened);
        Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        Assert.Equal(10000,book.Open(f.Business.Owner,saved.Id).Snapshot.Sources.LedgerClosingCents);
        Assert.Equal(9900,newer.Snapshot.Sources.LedgerClosingCents);
        Assert.False(newer.Reopened);
    }

    [Fact]
    public void LaterReconciliationsMustBeReopenedBeforeEarlierDependencies()
    {
        using var f=new LedgerFixture();
        ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);
        var january=Complete(f,book,new(ReconciliationPreviewTests.NoActivity(f),"Fictional January completion"));
        var statements=new StatementBook(f.Business.Store);
        var february=new StatementDraft(f.Accounts["1010"].Id,new(2027,2,1),new(2027,2,28),0,0,new([],"","{}"),"Fictional February no-activity statement");
        var imported=statements.Import(f.Business.Owner,february,statements.Preview(f.Business.Owner,february).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var feb=Complete(f,book,new(imported.Id,"Fictional February completion"));
        Assert.Contains(january.Id,feb.Snapshot.Sources.PriorReconciliationIds);
        Assert.Throws<ArgumentException>(()=>book.Reopen(f.Business.Owner,january.Id,"Fictional earlier correction",Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        string request=Guid.NewGuid().ToString();
        var reopen=book.Reopen(f.Business.Owner,feb.Id,"Fictional later review",request,BusinessFixture.Phrase);
        Assert.Equal("Fictional later review",book.ReopenHistory(f.Business.Owner,feb.Id).Single().Reason);
        Assert.Equal(f.Business.Owner.OwnerId,book.ReopenHistory(f.Business.Owner,feb.Id).Single().OwnerId);
        Assert.Equal(reopen.Id,book.Reopen(f.Business.Owner,feb.Id,"Fictional later review",request,BusinessFixture.Phrase).Id);
        book.Reopen(f.Business.Owner,january.Id,"Fictional earlier correction",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.All(book.History(f.Business.Owner),h=>Assert.True(h.Reopened));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM ReconciliationReopens;"));
    }

    [Theory]
    [InlineData("reconciliation-snapshot")]
    [InlineData("reconciliation-coverage")]
    [InlineData("audit")]
    public void InterruptedCompletionRollsBackSnapshotCoverageAndAuditAndCanRetry(string stage)
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);
        var book=new Reconciliations(f.Business.Store);
        var preview=book.Preview(f.Business.Owner,ready.Draft);
        string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected reconciliation completion failure");};
        Assert.Throws<IOException>(()=>book.Complete(f.Business.Owner,ready.Draft,preview.Sources.Watermark,request,BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM ReconciliationBankCoverage;"));
        book.FailurePoint=null;
        book.Complete(f.Business.Owner,ready.Draft,preview.Sources.Watermark,request,BusinessFixture.Phrase);
        Assert.Single(book.History(f.Business.Owner));
    }

    [Fact]
    public void FailedReopenKeepsTheOriginalLockAndSnapshotIntact()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);var book=new Reconciliations(f.Business.Store);
        var saved=Complete(f,book,ready.Draft);
        book.FailurePoint=p=>{if(p=="reconciliation-reopen")throw new IOException("Injected reopen failure");};
        Assert.Throws<IOException>(()=>book.Reopen(f.Business.Owner,saved.Id,"Fictional source review",Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.False(book.Open(f.Business.Owner,saved.Id).Reopened);
        Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Expense,1)));
    }

    [Fact]
    public void PriorBalanceContinuityCannotBeHiddenByAnOutstandingItem()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);var book=new Reconciliations(f.Business.Store);
        Complete(f,book,ready.Draft);
        var matching=new BankMatching(f.Business.Store);var match=matching.History(f.Business.Owner).Single();
        BankMatchingTests.Match(f,matching,new(2027,2,1),new MatchPortion(ready.Bank.Id,ready.Cash.Id,1,match.Id));
        var statements=new StatementBook(f.Business.Store);
        var bad=new StatementDraft(ready.Bank.AccountId,new(2027,2,1),new(2027,2,28),9999,9999,new([],"","{}"),"Fictional conflicting bank opening");
        var imported=statements.Import(f.Business.Owner,bad,statements.Preview(f.Business.Owner,bad).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var draft=new ReconciliationDraft(imported.Id,"Fictional contradictory opening review") with{Outstanding=[new(ready.Cash.Id,1,"Claimed outstanding residual","Fictional claimed evidence")]};
        var preview=book.Preview(f.Business.Owner,draft);
        Assert.Equal(0,preview.DifferenceCents);
        Assert.Single(preview.Sources.ContinuityWarnings);
        Assert.False(preview.ReadyToComplete);
        Assert.Throws<ArgumentException>(()=>book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
    }

    [Fact]
    public void OverlappingStatementAnchorsToThePriorClosingAndReusesItsCoverage()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);var book=new Reconciliations(f.Business.Store);
        var january=Complete(f,book,ready.Draft);
        var expense=f.Post(f.Draft(FinancialKind.Expense,1000) with{Date=new(2027,2,10)});
        var statements=new StatementBook(f.Business.Store);
        var source=StatementCsv.Parse("Date,Description,Amount\n2027-01-10,Fictional older deposit,100.00\n2027-02-10,Fictional February payment,-10.00",new());
        var overlap=new StatementDraft(ready.Bank.AccountId,new(2027,1,1),new(2027,2,28),0,9000,source);
        var preview=statements.Preview(f.Business.Owner,overlap);
        var imported=statements.Import(f.Business.Owner,overlap,preview.Generation,[new(2,ready.Bank.Id,"Same January transaction in an overlapping export")],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var payment=statements.SourceRows(f.Business.Owner,imported.Id).Single(r=>r.Source.SignedCents<0).Line;
        BankMatchingTests.Match(f,new BankMatching(f.Business.Store),new(2027,2,10),new MatchPortion(payment.Id,BankMatchingTests.Cash(f,expense).Id,1000));
        var completed=Complete(f,book,new(imported.Id,"Fictional overlapping February review"));
        Assert.Contains(january.Id,completed.Snapshot.Sources.PriorReconciliationIds);
        Assert.Single(completed.Snapshot.Sources.Bank);
        Assert.Equal(9000,completed.Snapshot.AdjustedLedgerCents);
    }

    [Fact]
    public void VersionTwentyTwoUpgradeRetainsSourceIdsAndSnapshotHashesDetectTampering()
    {
        using var f=new LedgerFixture();
        var ready=Ready(f);
        f.Business.Files.PrepareBankingVersion(22);
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var book=new Reconciliations(f.Business.Store);var saved=Complete(f,book,ready.Draft);
        Assert.Equal(ready.Bank.Id,saved.Snapshot.Sources.Bank.Single().Id);
        f.Business.Files.Execute("DROP TRIGGER Reconciliations_NoUpdate; UPDATE Reconciliations SET SnapshotJson='{}';");
        Assert.Throws<InvalidDataException>(()=>book.Open(f.Business.Owner,saved.Id));
    }
}
