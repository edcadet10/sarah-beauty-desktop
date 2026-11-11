using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReconciliationPreviewTests
{
    private static string Statement(LedgerFixture f,params long[] cents)
    {
        BankMatchingTests.Statement(f,cents);
        return new StatementBook(f.Business.Store).List(f.Business.Owner).Single().Id;
    }

    internal static string NoActivity(LedgerFixture f,long cents=0)
    {
        var book=new StatementBook(f.Business.Store);
        var draft=new StatementDraft(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),cents,cents,new([],"","{}"),"Fictional confirmed no-activity statement");
        var preview=book.Preview(f.Business.Owner,draft);
        return book.Import(f.Business.Owner,draft,preview.Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase).Id;
    }

    internal static void Zero(LedgerFixture f)=>new OpeningBalances(f.Business.Store).ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,
        new(2027,1,1),"Fictional evidenced starting zero",BusinessFixture.Phrase);

    [Fact]
    public void NetZeroUnexplainedBankItemsCannotMakeAReconciliationReady()
    {
        using var f=new LedgerFixture();
        Zero(f);
        string statement=Statement(f,2500,-2500);
        var preview=new Reconciliations(f.Business.Store).Preview(f.Business.Owner,new(statement,"Fictional net-zero review"));
        Assert.True(preview.Sources.OpeningConfirmed);
        Assert.Equal(0,preview.DifferenceCents);
        Assert.Equal(2,preview.UnexplainedBankCount);
        Assert.False(preview.ReadyToComplete);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void FullyMatchedSourcesAndConfirmedOpeningReconcileExactly()
    {
        using var f=new LedgerFixture();
        Zero(f);
        string statement=Statement(f,10000);
        var bank=new StatementBook(f.Business.Store).BankLines(f.Business.Owner,f.Accounts["1010"].Id).Single();
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        BankMatchingTests.Match(f,new BankMatching(f.Business.Store),f.Date,new MatchPortion(bank.Id,cash.Id,10000));
        var preview=new Reconciliations(f.Business.Store).Preview(f.Business.Owner,new(statement,"Fictional fully matched review"));
        Assert.True(preview.ReadyToComplete);
        Assert.Equal(10000,preview.AdjustedLedgerCents);
        Assert.Equal(10000,preview.AdjustedStatementCents);
        Assert.Equal("TEST",preview.Sources.Mode);
        Assert.True(preview.Sources.Watermark>0);
        Assert.Empty(preview.UnexplainedCashIds);
    }

    [Fact]
    public void OutstandingDepositsAndPaymentsNeedTheirFullRemaindersAndEvidence()
    {
        using var f=new LedgerFixture();
        Zero(f);
        string statement=NoActivity(f);
        var deposit=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var payment=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Expense,2500)));
        var book=new Reconciliations(f.Business.Store);
        var draft=new ReconciliationDraft(statement,"Fictional actual outstanding checks") with{Outstanding=[
            new(deposit.Id,10000,"Deposit in transit","Fictional deposit evidence"),new(payment.Id,2500,"Issued check not cleared","Fictional check evidence")]};
        var preview=book.Preview(f.Business.Owner,draft);
        Assert.True(preview.ReadyToComplete);
        Assert.Equal(10000,preview.OutstandingDepositsCents);
        Assert.Equal(2500,preview.OutstandingPaymentsCents);
        Assert.Equal(7500,preview.AdjustedStatementCents);
        var missing=book.Preview(f.Business.Owner,draft with{Outstanding=[draft.Outstanding[0]]});
        Assert.False(missing.ReadyToComplete);
        Assert.Single(missing.UnexplainedCashIds);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Outstanding=[draft.Outstanding[0] with{Cents=7500},draft.Outstanding[1]]}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Outstanding=[draft.Outstanding[0] with{EvidenceId=""},draft.Outstanding[1]]}));
    }

    [Fact]
    public void AlreadyPostedBookCorrectionsAreNotAddedToTheLedgerTwice()
    {
        using var f=new LedgerFixture();
        Zero(f);
        string statement=Statement(f,10000,-1000);
        var bank=new StatementBook(f.Business.Store).SourceRows(f.Business.Owner,statement).Select(r=>r.Line).ToArray();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,10000));
        var fee=f.Post(f.Draft(FinancialKind.Expense,1000) with{EvidenceId="Fictional actual bank fee"});
        var matching=new BankMatching(f.Business.Store);
        BankMatchingTests.Match(f,matching,f.Date,new(bank[0].Id,BankMatchingTests.Cash(f,receipt).Id,10000),new(bank[1].Id,BankMatchingTests.Cash(f,fee).Id,1000));
        var future=f.Post(f.Draft(FinancialKind.Expense,123) with{Date=f.Date.AddMonths(1),EvidenceId="Fictional later fee"});
        var book=new Reconciliations(f.Business.Store);
        var preview=book.Preview(f.Business.Owner,new(statement,"Fictional corrected books review"){PostedCorrectionIds=[fee.Id]});
        Assert.True(preview.ReadyToComplete);
        Assert.Equal(-1000,preview.PostedBookCorrectionsCents);
        Assert.Equal(10000,preview.LedgerBeforeCorrectionsCents);
        Assert.Equal(9000,preview.AdjustedLedgerCents);
        Assert.Equal(0,preview.DifferenceCents);
        Assert.DoesNotContain(preview.Sources.CorrectionChoices,c=>c.MovementId==future.Id);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,new(statement,"Wrong correction"){PostedCorrectionIds=[receipt.Id]}));
    }

    [Fact]
    public void UnknownAndFutureOpeningsAreNotConfirmedZero()
    {
        using var f=new LedgerFixture();
        string statement=NoActivity(f);
        var book=new Reconciliations(f.Business.Store);
        var draft=new ReconciliationDraft(statement,"Fictional opening review");
        Assert.False(book.Preview(f.Business.Owner,draft).ReadyToComplete);
        Zero(f);
        Assert.True(book.Preview(f.Business.Owner,draft).ReadyToComplete);
        var openings=new OpeningBalances(f.Business.Store);
        openings.RevokeZero(f.Business.Owner,openings.List(f.Business.Owner).Single().Id,"Fictional date correction",BusinessFixture.Phrase);
        openings.ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,new(2027,2,1),"Fictional later opening",BusinessFixture.Phrase);
        Assert.False(book.Preview(f.Business.Owner,draft).Sources.OpeningConfirmed);
    }

    [Fact]
    public void EvidencedOpeningIsInTheLedgerWithoutBeingAnotherDepositToMatch()
    {
        using var f=new LedgerFixture();
        string statement=NoActivity(f,10000);
        var opening=f.Post(f.Draft(FinancialKind.Opening,10000) with{CounterAccountId=f.Accounts["1010"].Id,Date=new(2027,1,1),EvidenceId="Fictional actual starting balance"});
        var book=new Reconciliations(f.Business.Store);
        var preview=book.Preview(f.Business.Owner,new(statement,"Fictional confirmed opening review"));
        Assert.True(preview.ReadyToComplete);
        Assert.Equal(10000,preview.Sources.LedgerClosingCents);
        Assert.Equal(10000,Assert.Single(preview.Sources.Cash).SignedCents);
        Assert.Equal(0,Assert.Single(preview.Sources.Cash).RemainingCents);
        var bank=BankMatchingTests.Statement(f,10000).Single();
        Assert.Throws<ArgumentException>(()=>BankMatchingTests.Match(f,new BankMatching(f.Business.Store),f.Date,new MatchPortion(bank.Id,BankMatchingTests.Cash(f,opening).Id,10000)));
        Assert.False(book.Preview(f.Business.Owner,new(statement,"Review after new evidence")).ReadyToComplete);
    }

    [Fact]
    public void UnsupportedForeignOrDuplicateOutstandingItemsAreRejected()
    {
        using var f=new LedgerFixture();
        Zero(f);
        string statement=NoActivity(f);
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));
        var foreign=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000) with{CashAccountId=f.Accounts["1020"].Id,EvidenceId="Fictional foreign-account source"}),"1020");
        var book=new Reconciliations(f.Business.Store);
        var item=new OutstandingCash(cash.Id,10000,"Fictional outstanding deposit","Fictional evidence");
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,new(statement,"Foreign item"){Outstanding=[item with{JournalLineId=foreign.Id}]}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,new(statement,"Duplicate item"){Outstanding=[item,item]}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,new(statement,"Foreign correction"){PostedCorrectionIds=[foreign.MovementId]}));
    }

    [Fact]
    public void LinkedCancellationHasNoInventedOutstandingPaymentAndItsCorrectionIsCountedOnce()
    {
        using var f=new LedgerFixture();
        Zero(f);
        string statement=NoActivity(f);
        var mistaken=f.Post(f.Draft(FinancialKind.Receipt,10000));
        var correction=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=mistaken.Id,EvidenceId="Fictional cancellation of mistaken posting"});
        var preview=new Reconciliations(f.Business.Store).Preview(f.Business.Owner,new(statement,"Fictional posting correction review"){PostedCorrectionIds=[correction.Id]});
        Assert.True(preview.ReadyToComplete);
        Assert.Equal(0,preview.Sources.LedgerClosingCents);
        Assert.Equal(0,preview.Sources.Cash.Sum(c=>c.SignedCents));
        Assert.Equal(10000,preview.LedgerBeforeCorrectionsCents);
        Assert.Equal(-10000,preview.PostedBookCorrectionsCents);
        Assert.Empty(preview.UnexplainedCashIds);
    }
}
