using System.Globalization;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class OpeningStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ANewConfirmedZeroKeepsItsGregorianDateAcrossReopen(string culture)
    {
        using var f=new LedgerFixture();WithCulture(culture,()=>new OpeningBalances(f.Business.Store).ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,f.Date,"Fictional evidenced zero opening",BusinessFixture.Phrase));Assert.Equal("2027-01-10",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Date FROM OpeningBalances;"));Assert.Equal(f.Date,Assert.Single(new OpeningBalances(f.Business.Store).List(f.Business.Owner)).Date);Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ExistingZeroHistoryAndRevocationKeepTheOriginalDate(string culture)
    {
        using var f=new LedgerFixture();var book=new OpeningBalances(f.Business.Store);book.ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,f.Date,"Fictional original opening review",BusinessFixture.Phrase);WithCulture(culture,()=>{var row=Assert.Single(book.List(f.Business.Owner));Assert.Equal(f.Date,row.Date);book.RevokeZero(f.Business.Owner,row.Id,"Fictional opening correction",BusinessFixture.Phrase);});var saved=Assert.Single(book.List(f.Business.Owner));Assert.Equal(f.Date,saved.Date);Assert.NotNull(saved.RevokedAt);Assert.False(new ChartOfAccounts(f.Business.Store).List(f.Business.Owner).Single(v=>v.Code=="1010").OpeningConfirmed);Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void AnOpeningBeforeTheFirstMovementIsAllowedAndALaterOpeningStillRejects(string culture)
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Expense,1234));var book=new OpeningBalances(f.Business.Store);WithCulture(culture,()=>{Assert.Contains("first ordinary movement",Assert.Throws<ArgumentException>(()=>book.ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,f.Date.AddDays(1),"Fictional invalid late opening",BusinessFixture.Phrase)).Message,StringComparison.Ordinal);book.ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,f.Date.AddDays(-1),"Fictional valid earlier opening",BusinessFixture.Phrase);});Assert.Equal(f.Date.AddDays(-1),Assert.Single(book.List(f.Business.Owner)).Date);Assert.Single(f.Book.Posted(f.Business.Owner));
    }
    private static void WithCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}finally{CultureInfo.CurrentCulture=previous;}
    }
}
