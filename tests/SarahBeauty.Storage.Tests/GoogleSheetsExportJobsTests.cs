using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsExportJobsTests
{
    [Fact]
    public async Task VerifiedReservationReopensAndReusesOneJobWithoutRenewingLockedOwnerOrWatermark()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await Stage(f);long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();
        var jobs=new GoogleSheetsExportJobs(f.Ledger.Business.Store);var job=jobs.Reserve(permit,artifact);
        var reopened=new BusinessStore(f.Ledger.Business.Store.DatabasePath);Assert.Equal(job,new GoogleSheetsExportJobs(reopened).Reserve(permit,artifact));
        Assert.Equal(job,Assert.Single(reopened.Read(db=>GoogleSheetsJobRecords.List(db,job.BusinessId))));Assert.Equal(watermark,f.Watermark);
        Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);Assert.Equal(artifact,GoogleSheetsJobRecords.Artifact(job));
        Assert.Throws<UnauthorizedAccessException>(()=>jobs.History(f.Ledger.Business.Owner));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("permission")]
    [InlineData("instance")]
    public async Task ChangesWhileWaitingForTheWriterPreventReservation(string change)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await Stage(f);using var cancellation=new CancellationTokenSource();var store=f.Ledger.Business.Store;
        store.BeforeTransactionReservation=()=>
        {
            store.BeforeTransactionReservation=null;
            if(change=="cancel")cancellation.Cancel();
            else if(change=="instance")f.Ledger.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");
            else f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision)).GetAwaiter().GetResult();
        };
        var error=Record.Exception(()=>new GoogleSheetsExportJobs(store).Reserve(permit,artifact,cancellation.Token));Assert.NotNull(error);
        if(change=="cancel")Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(0L,Convert.ToInt64(TestStore.Scalar(store.DatabasePath,"SELECT count(*) FROM AppMetadata WHERE Key GLOB 'GoogleSheetsJob/*';")));
        Assert.True(Directory.Exists(new ReadableGenerationPaths(store.DatabasePath).Generation(artifact.GenerationId)));
    }

    [Fact]
    public async Task CorruptStagedBytesCannotCreateAJobAndArePreservedForReview()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await Stage(f);var paths=new ReadableGenerationPaths(f.Ledger.Business.Store.DatabasePath);string file=paths.FilePath(paths.Generation(artifact.GenerationId),0);
        File.AppendAllText(file,"changed");Assert.Throws<InvalidDataException>(()=>new GoogleSheetsExportJobs(f.Ledger.Business.Store).Reserve(permit,artifact));
        Assert.EndsWith("changed",File.ReadAllText(file));Assert.Empty(f.Ledger.Business.Store.Read(db=>GoogleSheetsJobRecords.List(db,permit.Policy.BusinessId)));
    }

    [Fact]
    public async Task ChangedPersistedSourceCannotBeReusedOrAuthorizeTheOriginalJob()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await Stage(f);var store=f.Ledger.Business.Store;var jobs=new GoogleSheetsExportJobs(store);var job=jobs.Reserve(permit,artifact);
        store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$value WHERE Key=$key;",("$value",JsonSerializer.Serialize(job with{ManifestSha256=new string('A',64)})),("$key","GoogleSheetsJob/"+job.BusinessId+"/"+job.Id)));
        Assert.Throws<GoogleConnectionFailure>(()=>jobs.Reserve(permit,artifact));Assert.Throws<GoogleConnectionFailure>(()=>store.Read(db=>{jobs.Require(db,permit,job);return true;}));
    }

    [Fact]
    public async Task WrongOutputKindAndForeignBusinessCannotReserveOrReadHistory()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await Stage(f);using var foreign=new BusinessFixture();var jobs=new GoogleSheetsExportJobs(f.Ledger.Business.Store);
        Assert.Throws<UnauthorizedAccessException>(()=>jobs.Reserve(permit with{Kind=GoogleOutputKind.DriveArchive},artifact));
        Assert.Throws<UnauthorizedAccessException>(()=>jobs.History(foreign.Owner));
        Assert.Throws<GoogleConnectionFailure>(()=>jobs.Reserve(permit with{Policy=permit.Policy with{BusinessId=foreign.Owner.BusinessId}},artifact));
        Assert.Empty(f.Ledger.Business.Store.Read(db=>GoogleSheetsJobRecords.List(db,permit.Policy.BusinessId)));
    }

    internal static async Task<(GoogleOutputPermit Permit,ReadableGenerationArtifact Artifact)> Stage(GoogleOutputSettingsTests.Fixture f)
    {
        var created=await f.Create();await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,created.Revision));var permit=f.Settings.Authorize(GoogleOutputKind.SheetsExport);
        using var snapshot=new ReadableExportSnapshots(f.Ledger.Business.Store).Capture(permit);var selection=new ReadableExportSelection(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
        var generation=ReadableExportGeneration.Build(snapshot,selection);return(permit,new ReadableGenerationFiles(f.Ledger.Business.Store).Stage(generation));
    }
}
