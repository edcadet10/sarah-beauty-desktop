using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class StoreContentionTests
{
    [Fact]
    public async Task OwnerConfirmationCanFinishAfterAShortExclusiveWriterReleases()
    {
        using var f=new BusinessFixture();long watermark=Watermark(f);var access=new OwnerAccess(f.Store);var signedIn=await AfterWriterPause(f,()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));Assert.Equal(f.Owner.OwnerId,signedIn.OwnerId);Assert.Equal(f.Owner.BusinessId,signedIn.BusinessId);Assert.True(access.IsSessionCurrent(signedIn));signedIn.Lock();Assert.Equal(watermark,Watermark(f));
    }
    [Fact]
    public async Task FinancialApprovalAfterShortContentionCommitsOnlyOnce()
    {
        using var f=new LedgerFixture();var b=f.Business;var draft=f.Book.SaveDraft(b.Owner,f.Draft(FinancialKind.Expense,1234));long watermark=Watermark(b);var saved=await AfterWriterPause(b,()=>f.Book.Approve(b.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));Assert.Equal(saved.Id,f.Book.Approve(b.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);Assert.Single(f.Book.Posted(b.Owner));Assert.Equal(2,f.Book.Journal(b.Owner,saved.Id).Count);Assert.Equal(watermark+1,Watermark(b));Assert.Equal(1L,TestStore.Scalar(b.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Approve financial draft';"));
    }
    private static async Task<T> AfterWriterPause<T>(BusinessFixture f,Func<T> action)
    {
        using var blocker=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=f.Store.DatabasePath,Mode=SqliteOpenMode.ReadWrite,Pooling=false,ForeignKeys=true}.ToString());blocker.Open();using var command=blocker.CreateCommand();command.CommandText="BEGIN EXCLUSIVE;";command.ExecuteNonQuery();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var pending=Task.Factory.StartNew(()=>{started.SetResult();return action();},CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
        try{await started.Task;await Task.Delay(TimeSpan.FromMilliseconds(6500));}
        finally{command.CommandText="ROLLBACK;";command.ExecuteNonQuery();}
        return await pending;
    }
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
