using System.Globalization;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class BookPeriodStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void FirstCloseKeepsGregorianDatesAndReopensAfterChangingCalendars(string culture)
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);var book=new BookPeriods(f.Business.Store);var draft=Draft(1);string id="";
        WithCulture(culture,()=>{var preview=book.Preview(f.Business.Owner,draft);Assert.True(preview.ReadyToClose,string.Join("; ",preview.BlockingIssues));var saved=book.Close(f.Business.Owner,draft,preview.Reports.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase);id=saved.Id;Assert.Equal(new DateOnly(2027,1,31),book.ClosedThrough(f.Business.Owner));var summary=Assert.Single(book.History(f.Business.Owner));Assert.Equal(draft.From,summary.From);Assert.Equal(draft.Through,summary.Through);});
        Assert.Equal("2027-01-31",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT ClosedThrough FROM BookSettings;"));Assert.Equal("2027-01-01",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT FromDate FROM BookPeriodCloses;"));book.Reopen(f.Business.Owner,id,"Fictional calendar-independent reopen",Guid.NewGuid().ToString(),BusinessFixture.Phrase);Assert.Null(book.ClosedThrough(f.Business.Owner));Assert.True(book.Open(f.Business.Owner,id).Reopened);
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ReopeningALaterPeriodRestoresTheExactEarlierBoundary(string culture)
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);var book=new BookPeriods(f.Business.Store);BookPeriodTests.Close(f,book);BookPeriodTests.ReconcileMonth(f,2);var draft=Draft(2);string second="";
        WithCulture(culture,()=>{var preview=book.Preview(f.Business.Owner,draft);Assert.True(preview.ReadyToClose,string.Join("; ",preview.BlockingIssues));second=book.Close(f.Business.Owner,draft,preview.Reports.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase).Id;book.Reopen(f.Business.Owner,second,"Fictional later period correction",Guid.NewGuid().ToString(),BusinessFixture.Phrase);Assert.Equal(new DateOnly(2027,1,31),book.ClosedThrough(f.Business.Owner));});
        Assert.Equal("2027-01-31",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT ClosedThrough FROM BookSettings;"));Assert.Equal("2027-02-28",f.Business.Store.Read(db=>(string)db.Scalar("SELECT ThroughDate FROM BookPeriodCloses WHERE Id=$id;",("$id",second))!));Assert.True(book.Open(f.Business.Owner,second).Reopened);Assert.Single(f.Book.Posted(f.Business.Owner));
    }
    private static BookPeriodDraft Draft(int month)=>new(new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)),"Fictional calendar-independent close","Fictional retained closing evidence");
    private static void WithCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}finally{CultureInfo.CurrentCulture=previous;}
    }
}
