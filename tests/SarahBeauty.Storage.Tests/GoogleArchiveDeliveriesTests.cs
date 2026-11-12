using System.Text.Json;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleArchiveDeliveriesTests
{
    internal sealed class Fixture:IDisposable
    {
        internal readonly GoogleOutputSettingsTests.Fixture Google=new();
        internal LocalBackupJob Archive {get;private set;}=null!;
        internal GoogleOutputPermit Permit=>Google.Settings.Authorize(GoogleOutputKind.DriveArchive);
        internal GoogleArchiveDeliveries Deliveries=>new(Google.Ledger.Business.Store);
        internal static async Task<Fixture> Create()
        {
            var f=new Fixture();try
            {
                await f.Google.Create();await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(true,false,1));var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Google.Ledger.Business.Store).RegisterAsync(f.Google.Ledger.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);
                f.Archive=await new LocalBackups(f.Google.Ledger.Business.Store).CreateAsync(f.Google.Ledger.Business.Owner,Path.Combine(f.Google.Ledger.Business.Files.Folder,"fictional-google-source.age"));return f;
            }catch{f.Dispose();throw;}
        }
        internal GoogleArchiveDelivery Reserve(string driveId="fictional-drive-file")=>Deliveries.Reserve(Permit,Archive.Id,driveId);
        internal static GoogleUploadedArchive Result(GoogleArchiveDelivery delivery)=>new(delivery.DriveFileId,delivery.ByteLength,delivery.Sha256);
        public void Dispose()=>Google.Dispose();
    }
    [Fact]
    public async Task RealCompletedArchiveReservesOneDurableIdentityAndBackgroundCompletionPreservesLockedAccess()
    {
        using var f=await Fixture.Create();var delivery=f.Reserve();long watermark=f.Google.Watermark;Assert.Equal(delivery,f.Reserve("fictional-unused-generated-id"));Assert.Equal(f.Archive.Result!.Manifest.Watermark,delivery.ArchiveWatermark);Assert.Equal(f.Archive.Result.Sha256,delivery.Sha256);
        f.Google.Ledger.Business.Owner.Lock();var expiry=f.Google.Ledger.Business.Owner.ExpiresAt;var attempt=f.Deliveries.Begin(f.Permit,delivery);var completed=f.Deliveries.Complete(f.Permit,delivery,attempt,Fixture.Result(delivery));Assert.Equal("Succeeded",completed.Status);Assert.Equal(expiry,f.Google.Ledger.Business.Owner.ExpiresAt);Assert.Equal(watermark,f.Google.Watermark);
        var store=new BusinessStore(f.Google.Ledger.Business.Files.DatabasePath);var fresh=new OwnerAccess(store).SignIn(f.Google.Ledger.Business.Owner.OwnerId,BusinessFixture.Phrase);var reopened=Assert.Single(new GoogleArchiveDeliveries(store).History(fresh));Assert.Equal(delivery,reopened.Delivery);Assert.Equal(completed,Assert.Single(reopened.Attempts));
    }
    [Fact]
    public async Task FailedAndInterruptedAttemptsRemainSeparateAndPrivateDiagnosticsAreExcluded()
    {
        using var f=await Fixture.Create();var delivery=f.Reserve();var first=f.Deliveries.Begin(f.Permit,delivery);f.Deliveries.Fail(first,new GoogleConnectionFailure("Fictional-private-code","Fictional private provider detail"));var second=f.Deliveries.Begin(f.Permit,delivery);f.Deliveries.InterruptRunning(delivery);var third=f.Deliveries.Begin(f.Permit,delivery);
        Assert.Throws<GoogleConnectionFailure>(()=>f.Deliveries.Complete(f.Permit,delivery,third with{PolicyRevision=third.PolicyRevision+1},Fixture.Result(delivery)));Assert.Throws<InvalidDataException>(()=>f.Deliveries.Complete(f.Permit,delivery,third with{StartedAtUtc=DateTimeOffset.UtcNow.AddDays(-1).ToString("O")},Fixture.Result(delivery)));f.Deliveries.Complete(f.Permit,delivery,third,Fixture.Result(delivery));
        var history=Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner));Assert.Equal(3,history.Attempts.Length);Assert.Contains(history.Attempts,a=>a.Id==first.Id && a.Status=="Failed" && a.FailureCode=="GoogleOperationFailed");Assert.Contains(history.Attempts,a=>a.Id==second.Id && a.Status=="Interrupted" && a.FailureCode=="CompletionUnconfirmed");Assert.Contains(history.Attempts,a=>a.Id==third.Id && a.Status=="Succeeded");Assert.DoesNotContain("Fictional private",JsonSerializer.Serialize(history));
    }
    [Fact]
    public async Task WithdrawalPreventsSuccessButRetainsFailureAndLaterExplicitPermissionCanReuseTheFileIdentity()
    {
        using var f=await Fixture.Create();var delivery=f.Reserve();var permit=f.Permit;var attempt=f.Deliveries.Begin(permit,delivery);await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(false,false,permit.Policy.Revision));var error=Assert.Throws<GoogleConnectionFailure>(()=>f.Deliveries.Complete(permit,delivery,attempt,Fixture.Result(delivery)));f.Deliveries.Fail(attempt,error);
        Assert.DoesNotContain(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts,a=>a.Status=="Succeeded");await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(true,false,permit.Policy.Revision+1));Assert.Equal(delivery,f.Reserve("fictional-discarded-id"));var retry=f.Deliveries.Begin(f.Permit,delivery);Assert.True(retry.PolicyRevision>attempt.PolicyRevision);f.Deliveries.Complete(f.Permit,delivery,retry,Fixture.Result(delivery));Assert.Equal(2,Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts.Length);
    }
    [Theory]
    [InlineData("path")]
    [InlineData("instance")]
    [InlineData("recipient")]
    public async Task ChangedSourceFactsCannotCompleteEarlierDelivery(string defect)
    {
        using var f=await Fixture.Create();var delivery=f.Reserve();var permit=f.Permit;var attempt=f.Deliveries.Begin(permit,delivery);var changed=defect switch{"path"=>f.Archive with{Destination=Path.Combine(f.Google.Ledger.Business.Files.Folder,"fictional-other.age")},"instance"=>f.Archive with{InstanceId=Guid.NewGuid().ToString("N")},_=>f.Archive with{RecipientFingerprint=new string('A',64),Result=f.Archive.Result! with{RecipientFingerprint=new string('A',64)}}};
        f.Google.Ledger.Business.Store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(changed)),("$key",BackupJobRecords.Key(changed.Id))));Assert.Throws<GoogleConnectionFailure>(()=>f.Deliveries.Complete(permit,delivery,attempt,Fixture.Result(delivery)));Assert.Equal("Running",Assert.Single(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts).Status);
    }
    [Fact]
    public async Task ConcurrentReservationsCoalesceAndOnlyOneAttemptCanRunForTheDelivery()
    {
        using var f=await Fixture.Create();var permit=f.Permit;var reservations=await Task.WhenAll(Enumerable.Range(0,2).Select(i=>Task.Run(()=>f.Deliveries.Reserve(permit,f.Archive.Id,"fictional-file-"+i))));Assert.Equal(reservations[0],reservations[1]);
        var attempts=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{try{f.Deliveries.Begin(permit,reservations[0]);return true;}catch(GoogleConnectionFailure error) when(error.Code=="GoogleArchiveBusy"){return false;}})));Assert.Single(attempts,success=>success);Assert.Single(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts);
    }
}
