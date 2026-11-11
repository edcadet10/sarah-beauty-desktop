using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class StatementWithdrawalTests
{
    private static StatementDraft Draft(LedgerFixture f,bool flip=false,string suffix="")
    {
        string csv=$"Date,Description,Amount,Reference\n2027-01-10,Fictional deposit{suffix},100.00,fictional-bank-001";
        var mapping=new StatementCsvMapping{ReferenceColumn="Reference",ReliableReferences=true,NegateAmounts=flip};
        var source=StatementCsv.Parse(csv,mapping);
        return new(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),0,source.NetCents,source);
    }

    private static StatementImport Import(LedgerFixture f,StatementDraft draft,params StatementRowChoice[] choices)
    {
        var book=new StatementBook(f.Business.Store);
        return book.Import(f.Business.Owner,draft,book.Preview(f.Business.Owner,draft).Generation,choices,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }

    private static StatementWithdrawal Withdraw(LedgerFixture f,StatementImport statement)
    {
        long generation=new StatementBook(f.Business.Store).Preview(f.Business.Owner,Draft(f)).Generation;
        return new StatementWithdrawals(f.Business.Store).Approve(f.Business.Owner,statement.Id,generation,"Fictional wrong import mapping","Fictional corrected source evidence",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }

    [Fact]
    public void WithdrawnContentsAndReliableReferencesCanBeReimportedWithCorrectMapping()
    {
        using var f=new LedgerFixture();var book=new StatementBook(f.Business.Store);
        var original=Import(f,Draft(f,flip:true));var oldLine=book.SourceRows(f.Business.Owner,original.Id).Single();
        Withdraw(f,original);
        Assert.True(book.List(f.Business.Owner).Single().Withdrawn);
        Assert.True(book.SourceRows(f.Business.Owner,original.Id).Single().Line.Withdrawn);
        Assert.Equal(-10000,book.SourceRows(f.Business.Owner,original.Id).Single().Line.SignedCents);
        Assert.Empty(book.BankLines(f.Business.Owner,original.AccountId));
        Assert.Equal("",book.Preview(f.Business.Owner,Draft(f)).ExistingImportId);
        var corrected=Import(f,Draft(f));var newLine=book.SourceRows(f.Business.Owner,corrected.Id).Single();
        Assert.Equal(original.ContentHash,corrected.ContentHash);
        Assert.NotEqual(oldLine.Line.Id,newLine.Line.Id);
        Assert.Equal(10000,newLine.Line.SignedCents);
        Assert.Single(book.BankLines(f.Business.Owner,original.AccountId));
        Assert.Throws<ArgumentException>(()=>Import(f,Draft(f)));
        Assert.Throws<ArgumentException>(()=>Import(f,Draft(f,suffix:" in another export")));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("""
            INSERT INTO BankLines (Id,BusinessId,AccountId,Date,SignedCents,Description,Reference,ReliableReference)
            SELECT 'fictional-conflicting-bank-id',BusinessId,AccountId,Date,SignedCents,Description,Reference,ReliableReference
            FROM BankLines WHERE Id IN (SELECT BankLineId FROM StatementRows WHERE StatementId NOT IN (SELECT StatementId FROM StatementWithdrawals));
            """));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM StatementWithdrawals;"));
        Assert.Equal(0,f.Balance("1010"));Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ExactOriginalImportRetriesReturnTheWithdrawnRecordWithoutReactivatingIt()
    {
        using var f=new LedgerFixture();var book=new StatementBook(f.Business.Store);var draft=Draft(f);
        var preview=book.Preview(f.Business.Owner,draft);string request=Guid.NewGuid().ToString();
        var original=book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        Withdraw(f,original);
        var retry=book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        Assert.Equal(original.Id,retry.Id);Assert.True(retry.Withdrawn);
        Assert.Empty(book.BankLines(f.Business.Owner,original.AccountId));
    }

    [Fact]
    public void WithdrawingOneOverlapRetainsSharedBankLinesAndTheirValidMatches()
    {
        using var f=new LedgerFixture();var statements=new StatementBook(f.Business.Store);
        var first=Import(f,Draft(f));var line=statements.SourceRows(f.Business.Owner,first.Id).Single().Line;
        var second=Import(f,Draft(f,suffix:" overlapping source"),new StatementRowChoice(2,line.Id,"Same real bank transaction in another export"));
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var matching=new BankMatching(f.Business.Store);BankMatchingTests.Match(f,matching,f.Date,new MatchPortion(line.Id,cash.Id,10000));
        Withdraw(f,first);
        Assert.Equal(line.Id,statements.BankLines(f.Business.Owner,first.AccountId).Single().Id);
        Assert.Equal(0,matching.BankPositions(f.Business.Owner,first.AccountId,f.Date).Single().RemainingCents);
        Assert.False(statements.List(f.Business.Owner).Single(s=>s.Id==second.Id).Withdrawn);
        Assert.Throws<ArgumentException>(()=>Withdraw(f,second));
    }

    [Fact]
    public void OtherwiseUnreferencedLinesRequireAllMatchingToBeUndoneIncludingFutureMatches()
    {
        using var f=new LedgerFixture();var statement=Import(f,Draft(f));var statements=new StatementBook(f.Business.Store);
        var line=statements.SourceRows(f.Business.Owner,statement.Id).Single().Line;
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var matching=new BankMatching(f.Business.Store);var future=f.Date.AddMonths(1);
        BankMatchingTests.Match(f,matching,future,new MatchPortion(line.Id,cash.Id,10000));
        Assert.Throws<ArgumentException>(()=>Withdraw(f,statement));
        var match=matching.History(f.Business.Owner).Single();
        BankMatchingTests.Match(f,matching,future,new MatchPortion(line.Id,cash.Id,10000,match.Id));
        Withdraw(f,statement);
        Assert.Empty(matching.BankPositions(f.Business.Owner,line.AccountId,future));
        Assert.Equal(10000,matching.CashPositions(f.Business.Owner,line.AccountId,f.Date).Single().RemainingCents);
        Assert.Equal(2,matching.LoadSources(f.Business.Owner,line.AccountId,future).History.Count);
        Assert.True(matching.LoadSources(f.Business.Owner,line.AccountId,future).BankLines.Single().Withdrawn);
        Assert.Throws<ArgumentException>(()=>BankMatchingTests.Match(f,matching,future,new MatchPortion(line.Id,cash.Id,1)));
        var corrected=Import(f,Draft(f));var active=statements.SourceRows(f.Business.Owner,corrected.Id).Single().Line;
        BankMatchingTests.Match(f,matching,f.Date,new MatchPortion(active.Id,cash.Id,10000));
        Assert.Equal(0,matching.CashPositions(f.Business.Owner,line.AccountId,f.Date).Single().RemainingCents);
    }

    [Fact]
    public void CompletedReconciliationMustBeReopenedAndItsOriginalSnapshotRemainsReadable()
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var statement=Import(f,Draft(f));var line=new StatementBook(f.Business.Store).SourceRows(f.Business.Owner,statement.Id).Single().Line;
        var book=new Reconciliations(f.Business.Store);
        var saved=ReconciliationCloseTests.Complete(f,book,new(statement.Id,"Fictional completed source")
            {StatementErrors=[new(Guid.NewGuid().ToString(),line.Id,10000,"Fictional documented bank error","Fictional bank evidence")]});
        Assert.Throws<ArgumentException>(()=>Withdraw(f,statement));
        book.Reopen(f.Business.Owner,saved.Id,"Fictional mapping correction",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Withdraw(f,statement);
        Assert.Throws<ArgumentException>(()=>book.Sources(f.Business.Owner,statement.Id));
        Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        Assert.Equal(10000,book.Open(f.Business.Owner,saved.Id).Snapshot.Sources.Bank.Single().SignedCents);
    }

    [Fact]
    public void WithdrawalChecksOwnerFreshReviewEvidenceAndConflictingRetries()
    {
        using var f=new LedgerFixture();var statement=Import(f,Draft(f));var statements=new StatementBook(f.Business.Store);
        var book=new StatementWithdrawals(f.Business.Store);long generation=statements.Preview(f.Business.Owner,Draft(f)).Generation;
        string request=Guid.NewGuid().ToString();
        Assert.Throws<UnauthorizedAccessException>(()=>book.Approve(f.Business.Owner,statement.Id,generation,"Reason","Evidence",request,"Fictional wrong passphrase"));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,statement.Id,generation," ","Evidence",request,BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,statement.Id,generation,"Reason"," ",request,BusinessFixture.Phrase));
        Import(f,new(statement.AccountId,new(2027,2,1),new(2027,2,28),10000,10000,new([],"","{}"),"Fictional no-activity February"));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,statement.Id,generation,"Reason","Evidence",request,BusinessFixture.Phrase));
        generation=statements.Preview(f.Business.Owner,Draft(f)).Generation;
        var saved=book.Approve(f.Business.Owner,statement.Id,generation,"Reason","Evidence",request,BusinessFixture.Phrase);
        Assert.Equal(saved.Id,book.Approve(f.Business.Owner,statement.Id,generation,"Reason","Evidence",request.ToUpperInvariant(),BusinessFixture.Phrase).Id);
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,statement.Id,generation,"Changed reason","Evidence",request,BusinessFixture.Phrase));
        Assert.Single(book.History(f.Business.Owner));
        Assert.Equal(f.Business.Owner.OwnerId,saved.OwnerId);
    }

    [Theory]
    [InlineData("statement-withdrawal")]
    [InlineData("audit")]
    public void InterruptedWithdrawalKeepsTheOriginalActiveAndRetriesOnce(string stage)
    {
        using var f=new LedgerFixture();var statement=Import(f,Draft(f));var statements=new StatementBook(f.Business.Store);
        var book=new StatementWithdrawals(f.Business.Store);long generation=statements.Preview(f.Business.Owner,Draft(f)).Generation;string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected withdrawal failure");};
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,statement.Id,generation,"Reason","Evidence",request,BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));Assert.False(statements.List(f.Business.Owner).Single().Withdrawn);
        Assert.Single(statements.BankLines(f.Business.Owner,statement.AccountId));
        book.FailurePoint=null;
        book.Approve(f.Business.Owner,statement.Id,generation,"Reason","Evidence",request,BusinessFixture.Phrase);
        Assert.Single(book.History(f.Business.Owner));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Withdraw incorrect statement import';"));
    }

    [Fact]
    public void VersionTwentyFourUpgradePreservesImportedSourceAndSnapshotHistory()
    {
        using var f=new LedgerFixture();var statement=Import(f,Draft(f));
        var old=new StatementBook(f.Business.Store).SourceRows(f.Business.Owner,statement.Id).Single();
        f.Business.Files.PrepareBankingVersion(24);
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        Assert.Equal(old,new StatementBook(f.Business.Store).SourceRows(f.Business.Owner,statement.Id).Single());
        Withdraw(f,statement);Import(f,Draft(f,flip:true));
        Assert.Equal(2,new StatementBook(f.Business.Store).List(f.Business.Owner).Count);
    }
}
