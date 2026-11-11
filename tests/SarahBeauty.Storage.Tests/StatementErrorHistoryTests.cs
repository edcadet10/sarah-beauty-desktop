using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class StatementErrorHistoryTests
{
    private static StatementError Error(BankLine bank)=>new(Guid.NewGuid().ToString(),bank.Id,Math.Abs(bank.SignedCents),"Fictional bank-confirmed error","Fictional bank evidence");
    private static StatementErrorResolution Resolution(string error,BankLine bank,long cents)=>new(error,bank.Id,cents,"Fictional actual bank correction","Fictional correction evidence");

    private static (string Statement,BankLine Bank) Import(LedgerFixture f,int month,long opening,long change)
    {
        var from=new DateOnly(2027,month,1);var date=from.AddDays(9);var through=new DateOnly(2027,month,DateTime.DaysInMonth(2027,month));
        var source=StatementCsv.Parse($"Date,Description,Amount\n{date:yyyy-MM-dd},Fictional bank error source {month},{Money.Input(change)}",new());
        var draft=new StatementDraft(f.Accounts["1010"].Id,from,through,opening,opening+change,source);
        var book=new StatementBook(f.Business.Store);
        var saved=book.Import(f.Business.Owner,draft,book.Preview(f.Business.Owner,draft).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        return(saved.Id,book.SourceRows(f.Business.Owner,saved.Id).Single().Line);
    }

    [Fact]
    public void PartialBankCorrectionsCarryAcrossThreeCompletedStatementsWithoutCreatingMoney()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var jan=Import(f,1,0,2500);var error=Error(jan.Bank);
        var first=ReconciliationCloseTests.Complete(f,book,new(jan.Statement,"Fictional January error review"){StatementErrors=[error]});
        Assert.Equal(-2500,first.Snapshot.StatementErrorAdjustmentCents);
        Assert.True(first.Snapshot.ReadyToComplete);
        var feb=Import(f,2,2500,-1000);
        var second=ReconciliationCloseTests.Complete(f,book,new(feb.Statement,"Fictional partial correction"){BankCorrections=[Resolution(error.Id,feb.Bank,1000)]});
        Assert.Equal(-1500,second.Snapshot.StatementErrorAdjustmentCents);
        Assert.Equal(1500,second.Snapshot.RemainingStatementErrors.Single().RemainingCents);
        var mar=Import(f,3,1500,-1500);
        var third=ReconciliationCloseTests.Complete(f,book,new(mar.Statement,"Fictional final correction"){BankCorrections=[Resolution(error.Id,mar.Bank,1500)]});
        Assert.Equal(0,third.Snapshot.StatementErrorAdjustmentCents);
        Assert.Equal(0,third.Snapshot.RemainingStatementErrors.Single().RemainingCents);
        var positions=new BankMatching(f.Business.Store).BankPositions(f.Business.Owner,jan.Bank.AccountId,new(2027,3,31));
        Assert.All(positions,p=>Assert.Equal(0,p.RemainingCents));
        Assert.Equal(2500,positions.Single(p=>p.Id==jan.Bank.Id).ReservedForStatementErrorsCents);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(first.SnapshotHash,book.Open(f.Business.Owner,first.Id).SnapshotHash);
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE StatementErrors SET EvidenceId='Changed';"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM StatementErrorResolutions;"));
    }

    [Fact]
    public void ACorrectionCanShareABankLineWithARealMatchedBusinessPayment()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var jan=Import(f,1,0,2500);var error=Error(jan.Bank);
        ReconciliationCloseTests.Complete(f,book,new(jan.Statement,"Fictional initial error"){StatementErrors=[error]});
        var feb=Import(f,2,2500,-3000);
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Expense,500) with{Date=new(2027,2,10)}));
        BankMatchingTests.Match(f,new(f.Business.Store),cash.Date,new MatchPortion(feb.Bank.Id,cash.Id,500));
        var completed=ReconciliationCloseTests.Complete(f,book,new(feb.Statement,"Fictional mixed bank correction"){BankCorrections=[Resolution(error.Id,feb.Bank,2500)]});
        Assert.Equal(-500,completed.Snapshot.AdjustedLedgerCents);
        Assert.Equal(0,completed.Snapshot.UnexplainedBankCount);
        var position=new BankMatching(f.Business.Store).BankPositions(f.Business.Owner,feb.Bank.AccountId,new(2027,2,28)).Single(p=>p.Id==feb.Bank.Id);
        Assert.Equal(500,position.MatchedCents);
        Assert.Equal(2500,position.ReservedForStatementErrorsCents);
        Assert.Equal(0,position.RemainingCents);
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ErrorReservationsBlockFutureCashMatchingAndReleaseOnlyAfterReopening()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var jan=Import(f,1,0,2500);var error=Error(jan.Bank);var book=new Reconciliations(f.Business.Store);
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,1000) with{Date=new(2027,2,10)}));
        var matching=new BankMatching(f.Business.Store);var draft=new BankMatchDraft(cash.Date,"Fictional previously reviewed match",[new(jan.Bank.Id,cash.Id,1000)]);
        var oldReview=matching.Preview(f.Business.Owner,draft);
        var saved=ReconciliationCloseTests.Complete(f,book,new(jan.Statement,"Fictional reviewed bank error"){StatementErrors=[error]});
        Assert.Throws<ArgumentException>(()=>matching.Preview(f.Business.Owner,draft));
        Assert.Throws<ArgumentException>(()=>matching.Approve(f.Business.Owner,draft,oldReview.Generations,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Equal(2500,matching.BankPositions(f.Business.Owner,jan.Bank.AccountId,new(2027,1,30)).Single().RemainingCents);
        book.Reopen(f.Business.Owner,saved.Id,"Fictional reconsidered error evidence",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Equal(2500,matching.BankPositions(f.Business.Owner,jan.Bank.AccountId,cash.Date).Single().RemainingCents);
        Assert.Throws<ArgumentException>(()=>matching.Approve(f.Business.Owner,draft,oldReview.Generations,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        BankMatchingTests.Match(f,matching,cash.Date,new MatchPortion(jan.Bank.Id,cash.Id,1000));
        Assert.Equal(1500,matching.BankPositions(f.Business.Owner,jan.Bank.AccountId,cash.Date).Single().RemainingCents);
        Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
    }

    [Fact]
    public void APreviouslyApprovedFutureMatchCannotBeTakenByAnEarlierErrorReview()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var jan=Import(f,1,0,2500);var error=Error(jan.Bank);var book=new Reconciliations(f.Business.Store);
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,1000) with{Date=new(2027,2,10)}));
        var matching=new BankMatching(f.Business.Store);
        BankMatchingTests.Match(f,matching,cash.Date,new MatchPortion(jan.Bank.Id,cash.Id,1000));
        var draft=new ReconciliationDraft(jan.Statement,"Fictional earlier error review"){StatementErrors=[error]};
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));
        Assert.Throws<ArgumentException>(()=>book.Complete(f.Business.Owner,draft,book.Sources(f.Business.Owner,jan.Statement).Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        var match=matching.History(f.Business.Owner).Single();
        BankMatchingTests.Match(f,matching,cash.Date,new MatchPortion(jan.Bank.Id,cash.Id,1000,match.Id));
        Assert.True(ReconciliationCloseTests.Complete(f,book,draft).Snapshot.ReadyToComplete);
    }

    [Fact]
    public void AResolutionDependsOnItsOriginalActiveErrorAndReopenedSnapshotsKeepTheirEvidence()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var jan=Import(f,1,0,2500);var error=Error(jan.Bank);
        var first=ReconciliationCloseTests.Complete(f,book,new(jan.Statement,"Fictional initial error"){StatementErrors=[error]});
        var feb=Import(f,2,2500,-1000);var draft=new ReconciliationDraft(feb.Statement,"Fictional later correction"){BankCorrections=[Resolution(error.Id,feb.Bank,1000)]};
        var second=ReconciliationCloseTests.Complete(f,book,draft);
        Assert.Throws<ArgumentException>(()=>book.Reopen(f.Business.Owner,first.Id,"Fictional earlier revision",Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        book.Reopen(f.Business.Owner,second.Id,"Fictional revised correction",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Equal(1000,new BankMatching(f.Business.Store).BankPositions(f.Business.Owner,feb.Bank.AccountId,new(2027,2,28)).Single(p=>p.Id==feb.Bank.Id).RemainingCents);
        Assert.Equal(2500,book.Sources(f.Business.Owner,feb.Statement).CarriedErrors.Single().RemainingCents);
        book.Reopen(f.Business.Owner,first.Id,"Fictional revised original evidence",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));
        Assert.Equal(second.SnapshotHash,book.Open(f.Business.Owner,second.Id).SnapshotHash);
    }

    [Theory]
    [InlineData("statement-error")]
    [InlineData("statement-error-resolution")]
    [InlineData("audit")]
    public void InterruptedApprovalRollsBackErrorsCorrectionsReservationsAndSnapshot(string stage)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var lines=BankMatchingTests.Statement(f,2500,-2500);var error=Error(lines[0]);
        var statement=new StatementBook(f.Business.Store).List(f.Business.Owner).Single();
        var book=new Reconciliations(f.Business.Store);var draft=new ReconciliationDraft(statement.Id,"Fictional same-month correction")
            {StatementErrors=[error],BankCorrections=[Resolution(error.Id,lines[1],2500)]};
        var preview=book.Preview(f.Business.Owner,draft);string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected bank error approval failure");};
        Assert.Throws<IOException>(()=>book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,request,BusinessFixture.Phrase));
        foreach(string table in new[]{"StatementErrors","StatementErrorResolutions","Reconciliations","ReconciliationBankCoverage"})
            Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,$"SELECT COUNT(*) FROM {table};"));
        book.FailurePoint=null;
        var saved=book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,request,BusinessFixture.Phrase);
        var retry=draft with{StatementErrors=[error with{Id=error.Id.ToUpperInvariant()}],BankCorrections=[draft.BankCorrections[0] with{ErrorId=error.Id.ToUpperInvariant()}]};
        Assert.Equal(saved.Id,book.Complete(f.Business.Owner,retry,preview.Sources.Watermark,request,BusinessFixture.Phrase).Id);
        Assert.Throws<ArgumentException>(()=>book.Complete(f.Business.Owner,draft with{StatementErrors=[error with{EvidenceId="Changed evidence"}]},preview.Sources.Watermark,request,BusinessFixture.Phrase));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void VersionTwentyThreeUpgradePreservesLegacySnapshotsAndOriginalRequestRetries()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var draft=new ReconciliationDraft(ReconciliationPreviewTests.NoActivity(f),"Fictional legacy completion");var book=new Reconciliations(f.Business.Store);
        string request=Guid.NewGuid().ToString();var preview=book.Preview(f.Business.Owner,draft);
        var saved=book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,request,BusinessFixture.Phrase);
        MakeLegacySnapshot(f,saved.Id);
        string oldHash=(string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SnapshotHash FROM Reconciliations;")!;
        f.Business.Files.PrepareBankingVersion(23);
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var retry=book.Complete(f.Business.Owner,draft,preview.Sources.Watermark,request,BusinessFixture.Phrase);
        Assert.Equal(saved.Id,retry.Id);
        Assert.Equal(oldHash,retry.SnapshotHash);
        Assert.True(retry.Snapshot.ReadyToComplete);
        Assert.Empty(retry.Snapshot.Sources.CarriedErrors);
    }

    private static void MakeLegacySnapshot(LedgerFixture f,string id)
    {
        string raw=(string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SnapshotJson FROM Reconciliations;")!;
        var node=JsonNode.Parse(raw)!.AsObject();
        foreach(string key in new[]{"BankErrorClaims","StatementErrorAdjustmentCents","RemainingStatementErrors"})node.Remove(key);
        node["Draft"]!.AsObject().Remove("StatementErrors");node["Draft"]!.AsObject().Remove("BankCorrections");node["Sources"]!.AsObject().Remove("CarriedErrors");
        string json=node.ToJsonString();
        f.Business.Store.Transaction(db=>
        {
            db.Execute("DROP TRIGGER Reconciliations_NoUpdate;");
            db.Execute("UPDATE Reconciliations SET SnapshotJson=$json,SnapshotHash=$hash WHERE Id=$id;",("$json",json),("$hash",ReconciliationQueries.Hash(json)),("$id",id));
            db.Execute("CREATE TRIGGER Reconciliations_NoUpdate BEFORE UPDATE ON Reconciliations BEGIN SELECT RAISE(ABORT,'Immutable fixture snapshot'); END;");
            return true;
        });
    }
}
