using SarahBeauty.Business.Documents;

namespace SarahBeauty.Storage.Tests;

public sealed class QuoteDecisionDateTests
{
    private sealed class Clock(DateOnly day):TimeProvider
    {
        internal DateTimeOffset Utc=new(day.Year,day.Month,day.Day,12,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>Utc;
    }
    [Fact]
    public void FutureQuoteDecisionCannotChangeStatusAndExactRetrySurvivesClockRollback()
    {
        using var f=new BusinessFixture();var version=QuoteTerminalDecisionTests.Issue(f);var today=version.Snapshot.IssueDate.AddDays(1);var clock=new Clock(today);var book=new DocumentDecisions(f.Store,clock);var draft=QuoteTerminalDecisionTests.Draft(version,DocumentDecisionAction.DeclineQuote) with{DecisionDate=today.AddDays(1)};long watermark=Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        Assert.Throws<ArgumentException>(()=>book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Empty(book.History(f.Owner));Assert.Equal("Issued",Assert.Single(new QuoteBook(f.Store).Versions(f.Owner)).Status);Assert.Equal(watermark,TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        draft=draft with{DecisionDate=today};string request=Guid.NewGuid().ToString();var saved=book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,request);clock.Utc=clock.Utc.AddDays(-2);Assert.Equal(saved,book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,request));Assert.Equal(watermark+1,TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    }
}
