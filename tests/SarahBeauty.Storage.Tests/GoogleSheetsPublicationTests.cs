using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsPublicationTests
{
    [Fact]
    public async Task PublicationAndLiteralStatusRemainIdenticalAfterReopenAndRetry()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var deliveries=new GoogleSheetsDeliveries(store);var attempt=deliveries.Begin(permit,job);
        var creation=new GoogleSheetsCreationCheckpoints(store);creation.RecordFile(job,creation.BeginCreate(permit,job,creation.Prepare(permit,job)),"fictional-workbook");long watermark=f.Watermark;
        var publication=new GoogleSheetsPublicationCheckpoints(store).Prepare(permit,job,attempt);var generation=new ReadableGenerationFiles(store).Load(artifact);var table=GoogleSheetsDeliveryStatus.Build(job,publication,generation);
        deliveries.Fail(attempt,new IOException("Fictional private detail"));var next=deliveries.Begin(permit,job);f.Ledger.Business.Owner.Lock();
        var reopened=new BusinessStore(store.DatabasePath);var reread=new GoogleSheetsPublicationCheckpoints(reopened).Prepare(permit,job,next);Assert.Equal(publication,reread);Assert.Equal(table.Sha256,GoogleSheetsDeliveryStatus.Build(job,reread,generation).Sha256);
        Assert.Equal(31,table.Rows.Count);Assert.Equal(2,table.Columns.Count);Assert.Equal("Verified",table.Rows.Single(row=>row[0]=="Delivery state")[1]);Assert.Equal(163.ToString(),table.Rows.Single(row=>row[0]=="Source tables")[1]);
        var batch=Assert.Single(GoogleSheetsValueRequests.ForTables(job.GenerationId,job.SourceWatermark,[table]));using var json=JsonDocument.Parse(batch.Json);Assert.Equal("RAW",json.RootElement.GetProperty("valueInputOption").GetString());
        Assert.Equal("Running",next.Status);Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);
        Assert.Throws<InvalidDataException>(()=>GoogleSheetsDeliveryStatus.Build(job,publication with{ManifestSha256=new string('A',64)},generation));
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsDeliveryStatus.Build(job with{Selection=job.Selection with{FinancialThrough=new(2027,2,1)}},publication,generation));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("permission")]
    public async Task PublicationDoesNotAppearAfterLateWriterCancellationOrPermissionWithdrawal(string change)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var attempt=new GoogleSheetsDeliveries(store).Begin(permit,job);
        var creation=new GoogleSheetsCreationCheckpoints(store);creation.RecordFile(job,creation.BeginCreate(permit,job,creation.Prepare(permit,job)),"fictional-workbook");using var cancellation=new CancellationTokenSource();
        store.BeforeTransactionReservation=()=>{store.BeforeTransactionReservation=null;if(change=="cancel")cancellation.Cancel();else f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision)).GetAwaiter().GetResult();};
        Assert.NotNull(Record.Exception(()=>new GoogleSheetsPublicationCheckpoints(store).Prepare(permit,job,attempt,cancellation.Token)));Assert.Null(store.Read(db=>GoogleSheetsPublicationRecords.Get(db,job)));
    }
}
