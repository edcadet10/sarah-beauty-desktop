using System.Globalization;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class BankMatchErrorDateTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void MatchAndUnmatchKeepTheActualDateAndExactRetryCreatesNoExtraGroup(string culture)
    {
        using var f=new LedgerFixture();var bank=BankMatchingTests.Statement(f,2500).Single();
        var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,2500)));
        var book=new BankMatching(f.Business.Store);
        var draft=new BankMatchDraft(f.Date,"Fictional dated match",[new(bank.Id,cash.Id,2500)]);
        var preview=book.Preview(f.Business.Owner,draft);string request=Guid.NewGuid().ToString();
        InCulture(culture,()=>
        {
            var saved=book.Approve(f.Business.Owner,draft,preview.Generations,request,BusinessFixture.Phrase);
            long watermark=Watermark(f);
            Assert.Equal(saved.Id,book.Approve(f.Business.Owner,draft,preview.Generations,request,BusinessFixture.Phrase).Id);
            Assert.Equal(watermark,Watermark(f));
            var match=Assert.Single(book.History(f.Business.Owner));Assert.Equal(f.Date,match.Date);
            BankMatchingTests.Match(f,book,f.Date.AddDays(1),new MatchPortion(bank.Id,cash.Id,1000,match.Id));
            Assert.Equal(0,Assert.Single(book.BankPositions(f.Business.Owner,bank.AccountId,f.Date)).RemainingCents);
            Assert.Equal(1000,Assert.Single(book.BankPositions(f.Business.Owner,bank.AccountId,f.Date.AddDays(1))).RemainingCents);
        });
        Assert.Equal("2027-01-10",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT MIN(Date) FROM BankMatches;"));
        Assert.Equal("2027-01-11",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT MAX(Date) FROM BankMatches;"));
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void AStatementErrorIsReservedFromItsReconciliationDate(string culture)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var january=Import(f,1,0,2500);
        CompleteError(f,book,january);
        var matching=new BankMatching(f.Business.Store);
        InCulture(culture,()=>
        {
            Assert.Equal(0,Assert.Single(matching.BankPositions(f.Business.Owner,january.Bank.AccountId,new(2027,1,15))).ReservedForStatementErrorsCents);
            Assert.Equal(2500,Assert.Single(matching.BankPositions(f.Business.Owner,january.Bank.AccountId,new(2027,1,31))).ReservedForStatementErrorsCents);
        });
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void HistoricalCarriedErrorExcludesALaterBankCorrection(string culture)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var january=Import(f,1,0,2500);var error=CompleteError(f,book,january);
        var february=Import(f,2,2500,0);ReconciliationCloseTests.Complete(f,book,new(february.Statement,"Fictional February carried error review"));
        var march=Import(f,3,2500,-1000);
        ReconciliationCloseTests.Complete(f,book,new(march.Statement,"Fictional March partial bank correction")
            {BankCorrections=[new(error,march.Bank.Id,1000,"Fictional actual correction","Fictional correction evidence")]});
        InCulture(culture,()=>
        {
            var carried=Assert.Single(book.Sources(f.Business.Owner,february.Statement).CarriedErrors);
            Assert.Equal(2500,carried.RemainingCents);
            Assert.Equal(f.Date,carried.Date);
        });
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    private static string CompleteError(LedgerFixture f,Reconciliations book,(string Statement,BankLine Bank) source)
    {
        string id=Guid.NewGuid().ToString();
        ReconciliationCloseTests.Complete(f,book,new(source.Statement,"Fictional January bank-error review")
            {StatementErrors=[new(id,source.Bank.Id,2500,"Fictional bank-confirmed error","Fictional bank evidence")]});
        return id;
    }

    private static (string Statement,BankLine Bank) Import(LedgerFixture f,int month,long opening,long change)
    {
        var from=new DateOnly(2027,month,1);
        var rows=change==0?Array.Empty<StatementRow>():new[]{new StatementRow(2,from.AddDays(9),change,"Fictional bank error history","",false)};
        var draft=new StatementDraft(f.Accounts["1010"].Id,from,new(2027,month,DateTime.DaysInMonth(2027,month)),opening,opening+change,new(rows,"","{}"),"Fictional reviewed statement");
        var book=new StatementBook(f.Business.Store);
        var saved=book.Import(f.Business.Owner,draft,book.Preview(f.Business.Owner,draft).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        return(saved.Id,book.SourceRows(f.Business.Owner,saved.Id).Select(r=>r.Line).SingleOrDefault()!);
    }

    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static void InCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
