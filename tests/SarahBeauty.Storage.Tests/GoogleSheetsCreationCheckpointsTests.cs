using System.Text.Json;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsCreationCheckpointsTests
{
    [Fact]
    public async Task CreationIntentSurvivesReopenAndCannotBeIssuedTwice()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;
        var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var service=new GoogleSheetsCreationCheckpoints(store);long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();
        var prepared=service.Prepare(permit,job);Assert.Equal(prepared,service.Prepare(permit,job));var uncertain=service.BeginCreate(permit,job,prepared);
        var reopened=new GoogleSheetsCreationCheckpoints(new BusinessStore(store.DatabasePath));Assert.Equal(uncertain,reopened.Prepare(permit,job));
        Assert.Throws<InvalidDataException>(()=>reopened.BeginCreate(permit,job,prepared));Assert.Throws<InvalidDataException>(()=>reopened.BeginCreate(permit,job,uncertain));
        var known=reopened.RecordFile(job,uncertain,"fictional-created-workbook");Assert.Equal(known,reopened.Current(job));Assert.Equal(known,reopened.RecordFile(job,uncertain,known.FileId!));
        Assert.Throws<InvalidDataException>(()=>reopened.RecordFile(job,uncertain,"different-workbook"));Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);
    }

    [Fact]
    public async Task CompetingStoreHandlesCanIssueOnlyOneCreationIntent()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;
        var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var first=new GoogleSheetsCreationCheckpoints(store);
        var second=new GoogleSheetsCreationCheckpoints(new BusinessStore(store.DatabasePath));var prepared=first.Prepare(permit,job);
        var errors=await Task.WhenAll(Task.Run(()=>Record.Exception(()=>first.BeginCreate(permit,job,prepared))),Task.Run(()=>Record.Exception(()=>second.BeginCreate(permit,job,prepared))));
        Assert.Single(errors,error=>error is null);Assert.IsType<InvalidDataException>(Assert.Single(errors,error=>error is not null));Assert.Equal("CreateUncertain",first.Current(job)!.State);
    }

    [Fact]
    public async Task ReturnedFileIdentityIsRetainedAfterPermissionWithdrawalWithoutAuthorizingFurtherWork()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;
        var jobs=new GoogleSheetsExportJobs(store);var job=jobs.Reserve(permit,artifact);var service=new GoogleSheetsCreationCheckpoints(store);var uncertain=service.BeginCreate(permit,job,service.Prepare(permit,job));
        await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision));long watermark=f.Watermark;
        var known=service.RecordFile(job,uncertain,"fictional-returned-before-disable");Assert.Equal("FileKnown",known.State);Assert.Equal(watermark,f.Watermark);
        Assert.Throws<GoogleConnectionFailure>(()=>service.Prepare(permit,job));Assert.Throws<GoogleConnectionFailure>(()=>store.Read(db=>{jobs.Require(db,permit,job);return true;}));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("permission")]
    public async Task LateWriterChangesLeaveTheCreationPrepared(string change)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;
        var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var service=new GoogleSheetsCreationCheckpoints(store);var prepared=service.Prepare(permit,job);using var cancellation=new CancellationTokenSource();
        store.BeforeTransactionReservation=()=>{store.BeforeTransactionReservation=null;if(change=="cancel")cancellation.Cancel();else f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision)).GetAwaiter().GetResult();};
        Assert.NotNull(Record.Exception(()=>service.BeginCreate(permit,job,prepared,cancellation.Token)));Assert.Equal(prepared,service.Current(job));
    }

    [Theory]
    [InlineData("state")]
    [InlineData("file")]
    public async Task CorruptStateAndChangedJobCannotBeUsedToRecordAFile(string defect)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;
        var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var service=new GoogleSheetsCreationCheckpoints(store);var prepared=service.Prepare(permit,job);
        Assert.Throws<InvalidDataException>(()=>service.RecordFile(job,prepared,"fictional-file"));
        Assert.Throws<InvalidDataException>(()=>service.Current(job with{ManifestSha256=new string('A',64)}));
        var corrupt=defect=="state"?prepared with{State="CreateUncertain"}:prepared with{State="FileKnown",Revision=3,FileId="../outside"};
        store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$value WHERE Key=$key;",("$value",JsonSerializer.Serialize(corrupt)),("$key","GoogleSheetsCreation/"+job.BusinessId+"/"+job.Id)));
        Assert.Throws<InvalidDataException>(()=>service.Current(job));
    }
}
