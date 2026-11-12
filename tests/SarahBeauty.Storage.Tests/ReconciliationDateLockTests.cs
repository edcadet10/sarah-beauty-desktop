using System.Globalization;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReconciliationDateLockTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void CompletedAccountRejectsActualApprovalWithinItsCoveredDates(string culture)
    {
        using var f=new LedgerFixture();CompleteJanuary(f);
        var saved=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,100));
        long watermark=Watermark(f);
        InCulture(culture,()=>Assert.Contains("completed reconciliation",Assert.Throws<ArgumentException>(()=>
            f.Book.Approve(f.Business.Owner,saved.Id,saved.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal));
        Assert.Equal(watermark,Watermark(f));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void LaterCashApprovalRemainsWritableAfterACompletedAccountPeriod(string culture)
    {
        using var f=new LedgerFixture();CompleteJanuary(f);
        InCulture(culture,()=>f.Post(f.Draft(FinancialKind.Expense,100) with{Date=new(2027,2,1)}));
        Assert.Single(f.Book.Posted(f.Business.Owner));
        Assert.Equal(-100,f.Balance("1010"));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ANewStatementCannotIntroduceABankLineInsideTheCompletedPeriod(string culture)
    {
        using var f=new LedgerFixture();CompleteJanuary(f);
        var statements=new StatementBook(f.Business.Store);
        var draft=new StatementDraft(f.Accounts["1010"].Id,new(2027,1,1),new(2027,2,28),0,100,
            StatementCsv.Parse("Date,Description,Amount\n2027-01-15,Fictional newly discovered covered item,1.00",new()));
        var preview=statements.Preview(f.Business.Owner,draft);
        long watermark=Watermark(f);
        InCulture(culture,()=>Assert.Contains("completed reconciliation",Assert.Throws<ArgumentException>(()=>
            statements.Import(f.Business.Owner,draft,preview.Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase)).Message,StringComparison.Ordinal));
        Assert.Equal(watermark,Watermark(f));
        Assert.Single(statements.List(f.Business.Owner));
        Assert.Empty(statements.BankLines(f.Business.Owner,draft.AccountId));
    }

    private static void CompleteJanuary(LedgerFixture f)
    {
        ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);
        ReconciliationCloseTests.Complete(f,book,new(ReconciliationPreviewTests.NoActivity(f),"Fictional confirmed January account review"));
    }

    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));

    private static void InCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
