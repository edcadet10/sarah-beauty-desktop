using System.Globalization;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReconciliationStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ConsecutiveCompletionsStoreGregorianDatesAndRetainContinuity(string culture)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);
        InCulture(culture,()=>
        {
            var january=Complete(f,book,1);
            var february=Complete(f,book,2);
            Assert.Equal(new DateOnly(2027,1,31),january.AsOfDate);
            Assert.Equal(new DateOnly(2027,2,28),february.AsOfDate);
            Assert.Contains(january.Id,february.Snapshot.Sources.PriorReconciliationIds);
            Assert.Empty(february.Snapshot.Sources.ContinuityWarnings);
        });
        Assert.Equal("2027-02-28",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT MAX(AsOfDate) FROM Reconciliations;"));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ExistingHistoryAndRetainedSnapshotKeepTheirActualDates(string culture)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var saved=Complete(f,book,1);
        long watermark=Watermark(f);
        InCulture(culture,()=>
        {
            Assert.Equal(saved.AsOfDate,Assert.Single(book.History(f.Business.Owner)).AsOfDate);
            Assert.Equal(saved.SnapshotHash,book.Open(f.Business.Owner,saved.Id).SnapshotHash);
        });
        Assert.Equal(watermark,Watermark(f));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ReopeningStillRequiresLaterPeriodsFirstAndPreservesBothSnapshots(string culture)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var book=new Reconciliations(f.Business.Store);var january=Complete(f,book,1);var february=Complete(f,book,2);
        long watermark=Watermark(f);
        InCulture(culture,()=>
        {
            Assert.Contains("later reconciliations first",Assert.Throws<ArgumentException>(()=>Reopen(f,book,january.Id)).Message,StringComparison.Ordinal);
            Assert.Equal(watermark,Watermark(f));
            Reopen(f,book,february.Id);Reopen(f,book,january.Id);
            Assert.All(book.History(f.Business.Owner),r=>Assert.True(r.Reopened));
        });
        Assert.Equal(january.SnapshotHash,book.Open(f.Business.Owner,january.Id).SnapshotHash);
        Assert.Equal(february.SnapshotHash,book.Open(f.Business.Owner,february.Id).SnapshotHash);
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void HistoricalSourcesKeepTheirOpeningAndExcludeLaterEvidencedCorrections(string culture)
    {
        using var f=new LedgerFixture();ReconciliationPreviewTests.Zero(f);
        var earlier=f.Post(f.Draft(FinancialKind.Expense,100) with{EvidenceId="Fictional earlier correction evidence"});
        f.Post(f.Draft(FinancialKind.Expense,200) with{Date=new(2027,2,10),EvidenceId="Fictional later correction evidence"});
        string statement=ReconciliationPreviewTests.NoActivity(f);
        InCulture(culture,()=>
        {
            var source=new Reconciliations(f.Business.Store).Sources(f.Business.Owner,statement);
            Assert.True(source.OpeningConfirmed);
            Assert.Equal(-100,source.LedgerClosingCents);
            Assert.Equal(earlier.Id,Assert.Single(source.CorrectionChoices).MovementId);
        });
    }

    private static CompletedReconciliation Complete(LedgerFixture f,Reconciliations book,int month)
    {
        var statements=new StatementBook(f.Business.Store);
        var draft=new StatementDraft(f.Accounts["1010"].Id,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)),0,0,new([],"","{}"),"Fictional confirmed no-activity month");
        var saved=statements.Import(f.Business.Owner,draft,statements.Preview(f.Business.Owner,draft).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        return ReconciliationCloseTests.Complete(f,book,new(saved.Id,"Fictional dated completion review"));
    }

    private static void Reopen(LedgerFixture f,Reconciliations book,string id)=>book.Reopen(f.Business.Owner,id,"Fictional dated reopen review",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static void InCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
