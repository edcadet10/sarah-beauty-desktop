using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsDeliveryWorkerTests
{
    private static GoogleSheetsDeliveryWorker Worker(GoogleOutputSettingsTests.Fixture f,HttpClient client)=>new(f.Ledger.Business.Store,f.Connections,f.Settings,client){WaitForRequest=token=>{token.ThrowIfCancellationRequested();return Task.CompletedTask;}};
    [Fact]
    public async Task ActualCaptureAndPostedCellsAreIndependentlyReadAndPublishedWithoutUnlocking()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));var created=await f.Create();await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,created.Revision));var permit=f.Settings.Authorize(GoogleOutputKind.SheetsExport);
        using var server=new GoogleSheetsTestServer(f.Ledger.Business.Store,permit);using var client=new HttpClient(server);long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();
        var completed=await Worker(f,client).ExportCurrentAsync(new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31)));Assert.Equal("Succeeded",completed.Status);
        var store=new BusinessStore(f.Ledger.Business.Store.DatabasePath);var job=Assert.Single(store.Read(db=>GoogleSheetsJobRecords.List(db,permit.Policy.BusinessId)));var generation=new ReadableGenerationFiles(store).Load(GoogleSheetsJobRecords.Artifact(job));
        var publication=store.Read(db=>GoogleSheetsPublicationRecords.Get(db,job))!;var status=GoogleSheetsDeliveryStatus.Build(job,publication,generation);
        foreach(var table in generation.Tables.Append(status))
        {
            var rows=new[]{table.Columns}.Concat(table.Rows).ToArray();for(int r=0;r<rows.Length;r++)for(int c=0;c<rows[r].Count;c++)Assert.Equal(rows[r][c],server.Cells[(table.Name,r,c)]);
        }
        Assert.True(server.Published);Assert.Equal(1,server.Creates);Assert.Equal(1,server.SchemaWrites);Assert.Equal(1,server.PublicationWrites);Assert.True(server.GridReads>=164);
        Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);Assert.False(GoogleSheetsDeliveryWorker.IsActive(completed.Id));
    }
    [Theory]
    [InlineData("create")]
    [InlineData("schema")]
    [InlineData("values")]
    [InlineData("publication")]
    [InlineData("local completion")]
    public async Task LostResponsesAndLocalCompletionResumeTheSameWorkbook(string lost)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        using var server=new GoogleSheetsTestServer(store,permit){LostCreate=lost=="create",LostSchema=lost=="schema",LostValues=lost=="values",LostPublication=lost=="publication"};using var client=new HttpClient(server);var worker=Worker(f,client);
        worker.FailurePoint=point=>{if(lost=="local completion"&&point=="Remote readable export verified")throw new GoogleConnectionFailure("CompletionUnconfirmed","Fictional local completion failure");};
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>worker.RetryCurrentAsync(job.Id));int values=server.ValueWrites,schemas=server.SchemaWrites,publications=server.PublicationWrites;
        var completed=await Worker(f,client).RetryCurrentAsync(job.Id);Assert.Equal("Succeeded",completed.Status);Assert.Equal(1,server.Creates);Assert.Equal(1,server.SchemaWrites);Assert.Equal(1,server.PublicationWrites);
        if(lost is "publication" or "local completion"){Assert.Equal(values,server.ValueWrites);Assert.Equal(schemas,server.SchemaWrites);Assert.Equal(publications,server.PublicationWrites);}
        var attempts=store.Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId));Assert.Equal(2,attempts.Count);Assert.Contains(attempts,a=>a.Status=="Failed");Assert.Contains(attempts,a=>a.Status=="Succeeded");
    }
    [Theory]
    [InlineData("cell")]
    [InlineData("sharing")]
    [InlineData("permission")]
    [InlineData("late cancel")]
    [InlineData("late permission")]
    public async Task ChangedReadbackPrivacyPermissionAndFinalWriterCancellationCannotSucceed(string defect)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        using var server=new GoogleSheetsTestServer(store,permit);using var client=new HttpClient(server);using var cancellation=new CancellationTokenSource();var worker=Worker(f,client);
        worker.FailurePoint=point=>
        {
            if(point=="Source values sent")
            {
                if(defect=="cell"){var key=server.Cells.Keys.First();server.Cells[key]="Fictional changed cell";}
                if(defect=="sharing")server.Shared=true;
                if(defect=="permission")f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision)).GetAwaiter().GetResult();
            }
            if(defect is "late cancel" or "late permission"&&point=="Remote readable export verified")store.BeforeTransactionReservation=()=>{store.BeforeTransactionReservation=null;if(defect=="late cancel")cancellation.Cancel();else f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision)).GetAwaiter().GetResult();};
        };
        await Assert.ThrowsAnyAsync<Exception>(()=>worker.RetryCurrentAsync(job.Id,cancellation.Token));var attempt=Assert.Single(store.Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId)));
        Assert.Equal(defect=="late cancel"?"Cancelled":"Failed",attempt.Status);Assert.Equal(defect is "late cancel" or "late permission",server.Published);Assert.True(server.FileExists);
    }
    [Fact]
    public async Task LaterDesktopChangesDoNotRenewTheCapturedExportWatermark()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        using var server=new GoogleSheetsTestServer(store,permit);using var client=new HttpClient(server);var worker=Worker(f,client);
        worker.FailurePoint=point=>{if(point=="Source values sent")f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));};Assert.Equal("Succeeded",(await worker.RetryCurrentAsync(job.Id)).Status);
        Assert.True(f.Watermark>job.SourceWatermark);Assert.Equal(job,Assert.Single(store.Read(db=>GoogleSheetsJobRecords.List(db,job.BusinessId))));
        Assert.Equal(job.SourceWatermark,store.Read(db=>GoogleSheetsPublicationRecords.Get(db,job))!.SourceWatermark);
    }
    [Fact]
    public async Task EmptyLookupAfterLostCreationCannotDuplicateTheWorkbookAndLaterMatchCanResume()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        using var server=new GoogleSheetsTestServer(store,permit){LostCreate=true};using var client=new HttpClient(server);
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>Worker(f,client).RetryCurrentAsync(job.Id));server.EmptyLookup=true;
        Assert.Equal("ReadableWorkbookCreationUnconfirmed",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>Worker(f,client).RetryCurrentAsync(job.Id))).Code);Assert.Equal(1,server.Creates);Assert.Equal(0,server.ValueWrites);
        server.EmptyLookup=false;Assert.Equal("Succeeded",(await Worker(f,client).RetryCurrentAsync(job.Id)).Status);Assert.Equal(1,server.Creates);
    }
    [Fact]
    public async Task CorruptLocalSourceFailsBeforeHttpAndRetainsItsFailedAttempt()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        var paths=new ReadableGenerationPaths(store.DatabasePath);File.AppendAllText(paths.FilePath(paths.Generation(artifact.GenerationId),0),"changed");using var server=new GoogleSheetsTestServer(store,permit);using var client=new HttpClient(server);
        await Assert.ThrowsAsync<InvalidDataException>(()=>Worker(f,client).RetryCurrentAsync(job.Id));Assert.Equal(0,server.Calls);Assert.Equal("ReadableVerificationFailed",Assert.Single(store.Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId))).FailureCode);
    }
    [Fact]
    public async Task ChangedAlreadyPublishedCellsAreRejectedWithoutRepairOrAnotherMutation()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        using var server=new GoogleSheetsTestServer(store,permit);using var client=new HttpClient(server);var worker=Worker(f,client);worker.FailurePoint=point=>{if(point=="Remote readable export verified")throw new GoogleConnectionFailure("CompletionUnconfirmed","Fictional completion failure");};
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>worker.RetryCurrentAsync(job.Id));var key=server.Cells.Keys.First();server.Cells[key]="Fictional changed published value";int values=server.ValueWrites,schemas=server.SchemaWrites,publications=server.PublicationWrites;
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>Worker(f,client).RetryCurrentAsync(job.Id));Assert.Equal(values,server.ValueWrites);Assert.Equal(schemas,server.SchemaWrites);Assert.Equal(publications,server.PublicationWrites);Assert.Equal("Fictional changed published value",server.Cells[key]);
        Assert.DoesNotContain(store.Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId)),attempt=>attempt.Status=="Succeeded");
    }
    [Fact]
    public async Task ExclusiveLeaseBlocksHttpAndAbandonedRunningAttemptBecomesInterrupted()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);new GoogleSheetsDeliveries(store).Begin(permit,job);
        using var server=new GoogleSheetsTestServer(store,permit);using var client=new HttpClient(server);
        using(var lease=GoogleSheetsDeliveryLease.TryAcquire(store)){Assert.NotNull(lease);Assert.Equal("GoogleSheetsBusy",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>Worker(f,client).RetryCurrentAsync(job.Id))).Code);Assert.Equal(0,server.Calls);}
        Assert.Equal("Succeeded",(await Worker(f,client).RetryCurrentAsync(job.Id)).Status);Assert.Contains(store.Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId)),a=>a.Status=="Interrupted");
    }
}
