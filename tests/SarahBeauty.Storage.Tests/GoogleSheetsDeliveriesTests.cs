using System.Text.Json;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsDeliveriesTests
{
    [Fact]
    public async Task FailedInterruptedAndVerifiedAttemptsReopenWithoutPrivateProviderDetailsOrWatermarkChanges()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var deliveries=new GoogleSheetsDeliveries(store);long watermark=f.Watermark;
        var first=deliveries.Begin(permit,job);deliveries.Fail(first,new GoogleConnectionFailure("fictional-private-code","Fictional private provider details"));
        var second=deliveries.Begin(permit,job);Assert.Throws<GoogleConnectionFailure>(()=>deliveries.Begin(permit,job));deliveries.InterruptRunning();var third=deliveries.Begin(permit,job);
        var checkpoints=new GoogleSheetsCreationCheckpoints(store);checkpoints.RecordFile(job,checkpoints.BeginCreate(permit,job,checkpoints.Prepare(permit,job)),"fictional-verified-file");
        Assert.Equal(3,Assert.Single(deliveries.History(f.Ledger.Business.Owner)).Attempts.Length);
        new GoogleSheetsPublicationCheckpoints(store).Prepare(permit,job,third);
        f.Ledger.Business.Owner.Lock();var result=new GoogleSheetsVerifiedDelivery("fictional-verified-file",job.GenerationId,job.ManifestSha256,job.SourceWatermark,DateTimeOffset.UtcNow.ToString("O"));var completed=deliveries.Complete(permit,job,third,result);
        Assert.Equal("Succeeded",completed.Status);Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);
        var attempts=new BusinessStore(store.DatabasePath).Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId));Assert.Equal(3,attempts.Count);
        Assert.Contains(attempts,a=>a.Id==first.Id&&a.FailureCode=="GoogleOperationFailed");Assert.Contains(attempts,a=>a.Id==second.Id&&a.Status=="Interrupted");Assert.Contains(completed,attempts);
        Assert.DoesNotContain("Fictional private",JsonSerializer.Serialize(attempts));Assert.Throws<GoogleConnectionFailure>(()=>deliveries.Begin(permit,job));
        Assert.Throws<InvalidDataException>(()=>deliveries.Fail(third,new IOException()));Assert.Throws<UnauthorizedAccessException>(()=>deliveries.History(f.Ledger.Business.Owner));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("permission")]
    [InlineData("file")]
    [InlineData("digest")]
    [InlineData("attempt")]
    public async Task LateWriterChangesAndMismatchedProofCannotRecordSuccess(string defect)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var deliveries=new GoogleSheetsDeliveries(store);var attempt=deliveries.Begin(permit,job);
        var checkpoints=new GoogleSheetsCreationCheckpoints(store);checkpoints.RecordFile(job,checkpoints.BeginCreate(permit,job,checkpoints.Prepare(permit,job)),"fictional-file");
        var result=new GoogleSheetsVerifiedDelivery("fictional-file",job.GenerationId,job.ManifestSha256,job.SourceWatermark,DateTimeOffset.UtcNow.ToString("O"));using var cancellation=new CancellationTokenSource();
        new GoogleSheetsPublicationCheckpoints(store).Prepare(permit,job,attempt);
        if(defect is "cancel" or "permission")store.BeforeTransactionReservation=()=>{store.BeforeTransactionReservation=null;if(defect=="cancel")cancellation.Cancel();else f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision)).GetAwaiter().GetResult();};
        if(defect=="file")result=result with{FileId="different-file"};if(defect=="digest")result=result with{ManifestSha256=new string('A',64)};
        var supplied=defect=="attempt"?attempt with{PolicyRevision=attempt.PolicyRevision+1}:attempt;
        Assert.NotNull(Record.Exception(()=>deliveries.Complete(permit,job,supplied,result,cancellation.Token)));
        Assert.Equal("Running",Assert.Single(store.Read(db=>GoogleSheetsAttemptRecords.List(db,job.BusinessId))).Status);
        var failed=deliveries.Fail(attempt,new OperationCanceledException());Assert.Equal("Cancelled",failed.Status);
    }
}
