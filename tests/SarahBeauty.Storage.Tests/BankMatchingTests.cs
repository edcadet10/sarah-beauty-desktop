using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BankMatchingTests
{
    internal static BankLine[] Statement(LedgerFixture f,params long[] signed)
    {
        string csv="Date,Description,Amount\n"+string.Join('\n',signed.Select((c,i)=>$"{f.Date:yyyy-MM-dd},Fictional bank row {i+1},{Money.Input(c)}"));
        var source=StatementCsv.Parse(csv,new());
        var draft=new StatementDraft(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),0,source.NetCents,source);
        var book=new StatementBook(f.Business.Store);
        var preview=book.Preview(f.Business.Owner,draft);
        var choices=preview.Rows.Where(r=>r.SimilarInSource || r.Candidates.Length>0).Select(r=>new StatementRowChoice(r.Row.SourceRow,Reason:"Distinct fictional bank fixture transaction")).ToArray();
        var imported=book.Import(f.Business.Owner,draft,preview.Generation,choices,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        return book.SourceRows(f.Business.Owner,imported.Id).Select(r=>r.Line).ToArray();
    }

    internal static CashMatchLine Cash(LedgerFixture f,PostedMovement source,string code="1010")=>
        new BankMatching(f.Business.Store).CashLines(f.Business.Owner,f.Accounts[code].Id).Single(c=>c.MovementId==source.Id);

    internal static BankMatchGroup Match(LedgerFixture f,BankMatching book,DateOnly date,params MatchPortion[] portions)
    {
        var draft=new BankMatchDraft(date,"Fictional reviewed matching decision",portions);
        var preview=book.Preview(f.Business.Owner,draft);
        return book.Approve(f.Business.Owner,draft,preview.Generations,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }

    [Fact]
    public void PartialAndCombinedMatchesConsumeBothSidesWithoutChangingTheBooks()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000,5000);
        var first=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,12000)));
        var second=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,3000)));
        var book=new BankMatching(f.Business.Store);
        Match(f,book,f.Date,new(bank[0].Id,first.Id,10000),new(bank[1].Id,first.Id,2000),new(bank[1].Id,second.Id,3000));
        Assert.All(book.BankPositions(f.Business.Owner,first.AccountId,f.Date),p=>Assert.Equal(0,p.RemainingCents));
        Assert.All(book.CashPositions(f.Business.Owner,first.AccountId,f.Date),p=>Assert.Equal(0,p.RemainingCents));
        Assert.Equal(15000,f.Balance("1010"));
        Assert.Equal(-15000,f.Balance("4000"));
        Assert.Equal(3,book.History(f.Business.Owner).Count);
        Assert.Equal(f.Business.Owner.OwnerId,Assert.Single(book.Approvals(f.Business.Owner)).ApprovedBy);
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void ExcessAcrossAGroupWrongDirectionAndForeignAccountsCannotMatch()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var one=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,7000)));
        var two=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,7000)));
        var expense=Cash(f,f.Post(f.Draft(FinancialKind.Expense,1000)));
        var clearing=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,1000) with{CashAccountId=f.Accounts["1020"].Id}),"1020");
        var book=new BankMatching(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,new(f.Date,"Fictional grouped excess",[new(bank.Id,one.Id,7000),new(bank.Id,two.Id,7000)])));
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new(bank.Id,one.Id,7000),new(bank.Id,two.Id,7000)));
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,expense.Id,1)));
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,clearing.Id,1)));
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,one.Id,7001)));
        Assert.Empty(book.History(f.Business.Owner));
    }

    [Fact]
    public void PartialUnmatchesRestoreAmountsOnceAndCannotUndoAnUnmatch()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var book=new BankMatching(f.Business.Store);
        Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,10000));
        var original=book.History(f.Business.Owner).Single();
        Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,6000,original.Id));
        Assert.Equal(6000,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,4001,original.Id)));
        var unmatch=book.History(f.Business.Owner).Single(r=>r.ReversesId.Length>0);
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,1,unmatch.Id)));
        Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,4000,original.Id));
        Assert.Equal(10000,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
        Assert.Equal(10000,Assert.Single(book.CashPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
    }

    [Fact]
    public void ExactRetriesReturnOneApprovalAndCompetingStaleReviewsCannotOvermatch()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var book=new BankMatching(f.Business.Store);
        var draft=new BankMatchDraft(f.Date,"Fictional competing matching",[new(bank.Id,cash.Id,6000)]);
        var preview=book.Preview(f.Business.Owner,draft);
        string request=Guid.NewGuid().ToString();
        var saved=book.Approve(f.Business.Owner,draft,preview.Generations,request,BusinessFixture.Phrase);
        Assert.Equal(saved.Id,book.Approve(f.Business.Owner,draft,preview.Generations,request,BusinessFixture.Phrase).Id);
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,draft,preview.Generations,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,draft with{Reason="Different request contents"},preview.Generations,request,BusinessFixture.Phrase));
        Assert.Single(book.History(f.Business.Owner));
        Assert.Equal(4000,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
    }

    [Fact]
    public void FutureMatchReservationsProtectHistoricalBounds()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var book=new BankMatching(f.Business.Store);
        Match(f,book,f.Date.AddDays(2),new MatchPortion(bank.Id,cash.Id,6000));
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date.AddDays(1),new MatchPortion(bank.Id,cash.Id,6000)));
        Match(f,book,f.Date.AddDays(1),new MatchPortion(bank.Id,cash.Id,4000));
        Assert.Equal(6000,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date.AddDays(1))).RemainingCents);
        Assert.Equal(0,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date.AddDays(2))).RemainingCents);
    }

    [Fact]
    public void ActiveAndDatedMatchesMustBeUndoneBeforeReversingCash()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var source=f.Post(f.Draft(FinancialKind.Receipt,10000));
        var cash=Cash(f,source);
        var book=new BankMatching(f.Business.Store);
        Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,10000));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id}));
        var match=book.History(f.Business.Owner).Single();
        Match(f,book,f.Date.AddDays(2),new MatchPortion(bank.Id,cash.Id,10000,match.Id));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id,Date=f.Date.AddDays(1)}));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id,Date=f.Date.AddDays(2)});
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date.AddDays(3),new MatchPortion(bank.Id,cash.Id,1)));
        Assert.Equal(0,f.Balance("1010"));
    }

    [Fact]
    public void APostingCancellationIsNotAnotherBankTransactionButActualRefundsCanMatch()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000,-10000);
        var mistaken=f.Post(f.Draft(FinancialKind.Receipt,10000));
        var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=mistaken.Id});
        var undoCash=Cash(f,reversal);
        var book=new BankMatching(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank[1].Id,undoCash.Id,10000)));
        Assert.All(book.CashPositions(f.Business.Owner,undoCash.AccountId,f.Date),p=>Assert.Equal(0,p.RemainingCents));
        var received=f.Post(f.Draft(FinancialKind.Receipt,10000));
        var refunded=f.Post(f.Draft(FinancialKind.Refund,10000) with{OriginalId=received.Id});
        Match(f,book,f.Date,new(bank[0].Id,Cash(f,received).Id,10000),new(bank[1].Id,Cash(f,refunded).Id,10000));
        Assert.All(book.BankPositions(f.Business.Owner,undoCash.AccountId,f.Date),p=>Assert.Equal(0,p.RemainingCents));
        Assert.Equal(0,f.Balance("1010"));
    }

    [Fact]
    public void ProcessorReceiptMatchesItsActualNetCashRatherThanCustomerGross()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,11640).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,12000) with{FeeCents=360}));
        var book=new BankMatching(f.Business.Store);
        Assert.Equal(11640,cash.SignedCents);
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,12000)));
        Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,11640));
        Assert.Equal(0,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
        Assert.Equal(-12000,f.Balance("4000"));
        Assert.Equal(360,f.Balance("5020"));
    }

    [Theory]
    [InlineData("match-group")]
    [InlineData("bank-match")]
    [InlineData("audit")]
    public void FailureRollsBackApprovalAndSourceGenerationsAndRetryCreatesOneMatch(string stage)
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var book=new BankMatching(f.Business.Store);
        var draft=new BankMatchDraft(f.Date,"Fictional interrupted matching",[new(bank.Id,cash.Id,10000)]);
        var preview=book.Preview(f.Business.Owner,draft);
        string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected matching failure");};
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,draft,preview.Generations,request,BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));
        book.FailurePoint=null;
        book.Approve(f.Business.Owner,draft,preview.Generations,request,BusinessFixture.Phrase);
        Assert.Single(book.History(f.Business.Owner));
    }

    [Fact]
    public void RepeatedMaximumValueMatchesAndUnmatchesKeepExactAvailability()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,long.MaxValue).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,long.MaxValue)));
        var book=new BankMatching(f.Business.Store);
        for(int i=0;i<3;i++)
        {
            var group=Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,long.MaxValue));
            var original=book.History(f.Business.Owner).Single(r=>r.GroupId==group.Id);
            Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,long.MaxValue,original.Id));
        }
        Assert.Equal(long.MaxValue,Assert.Single(book.BankPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
        Assert.Equal(long.MaxValue,Assert.Single(book.CashPositions(f.Business.Owner,cash.AccountId,f.Date)).RemainingCents);
    }

    [Fact]
    public void ClosedDatesAndDatesBeforeSourcesCannotBeMatched()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000) with{Date=f.Date.AddDays(1)}));
        var book=new BankMatching(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,10000)));
        f.Business.Files.Execute("UPDATE BookSettings SET ClosedThrough='2027-01-31';");
        Assert.Throws<ArgumentException>(()=>Match(f,book,f.Date.AddDays(1),new MatchPortion(bank.Id,cash.Id,10000)));
        Assert.Empty(book.History(f.Business.Owner));
    }

    [Fact]
    public void VersionTwentyUpgradeKeepsStatementIdentitiesAndMatchHistoryIsImmutable()
    {
        using var f=new LedgerFixture();
        var bank=Statement(f,10000).Single();
        var cash=Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        f.Business.Files.PrepareBankingVersion(20);
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var book=new BankMatching(f.Business.Store);
        Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,10000));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE BankMatches SET Cents=1;"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM BankMatchGroups;"));
        Assert.Equal(bank.Id,book.History(f.Business.Owner).Single().BankLineId);
    }
}
