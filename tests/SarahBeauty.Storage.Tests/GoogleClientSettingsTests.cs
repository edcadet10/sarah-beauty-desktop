using System.Text;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleClientSettingsTests
{
    internal static byte[] Client(string suffix="first")=>Encoding.UTF8.GetBytes("{\"installed\":{\"client_id\":\"123456789-fictional.apps.googleusercontent.com\",\"client_secret\":\"Fictional-TEST-secret-"+suffix+"\",\"project_id\":\"fictional-test-project\",\"auth_uri\":\"https://accounts.google.com/o/oauth2/auth\",\"token_uri\":\"https://oauth2.googleapis.com/token\"}}");
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
    [Fact]
    public void SavedClientReopensProtectedWithPublicProvenanceAndNoArchiveSecret()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);long watermark=Watermark(f);byte[] json=Client();
        Assert.Null(settings.Current(f.Business.Owner));var saved=settings.Save(f.Business.Owner,json);Assert.Equal(watermark+1,Watermark(f));Assert.Equal(f.Business.Store.InstanceId,saved.InstanceId);Assert.Equal(1,saved.Revision);
        var reopened=new GoogleClientSettings(new BusinessStore(f.Business.Files.DatabasePath),privateFiles.Folder);Assert.Equal(saved,reopened.Current(f.Business.Owner));Assert.Equal("Fictional-TEST-secret-first",reopened.Load()!.Client.ClientSecret);
        Assert.Equal(saved,settings.Save(f.Business.Owner,json,saved.Id));Assert.Equal(watermark+1,Watermark(f));
        using var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner);Assert.Equal(-1,File.ReadAllBytes(captured.FilePath("sarahbeauty.db")).AsSpan().IndexOf(Encoding.UTF8.GetBytes("Fictional-TEST-secret")));Assert.Single(captured.Manifest.Files);
        var changed=settings.Save(f.Business.Owner,Client("second"),saved.Id);Assert.NotEqual(saved.Id,changed.Id);Assert.Equal(2,changed.Revision);Assert.Equal(watermark+2,Watermark(f));Assert.Equal("Fictional-TEST-secret-second",settings.Load()!.Client.ClientSecret);
    }
    [Fact]
    public void ForeignLockedInvalidAndStaleRequestsPreserveTheExistingFileAndMetadata()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);
        Assert.Throws<UnauthorizedAccessException>(()=>settings.Save(foreign.Owner,Client()));Assert.Throws<InvalidDataException>(()=>settings.Save(f.Business.Owner,Encoding.UTF8.GetBytes("Fictional invalid input")));var saved=settings.Save(f.Business.Owner,Client());long watermark=Watermark(f);
        Assert.Throws<ArgumentException>(()=>settings.Save(f.Business.Owner,Client("stale")));Assert.Equal("Fictional-TEST-secret-first",settings.Load()!.Client.ClientSecret);Assert.Equal(watermark,Watermark(f));
        f.Business.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>settings.Save(f.Business.Owner,Client("locked"),saved.Id));Assert.Equal("Fictional-TEST-secret-first",settings.Load()!.Client.ClientSecret);
    }
    [Fact]
    public void HumanLockDuringFlushCannotCommitClientSettingsOrAnAudit()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);long watermark=Watermark(f);
        settings.FailurePoint=point=>{if(point=="Protected file flushed")f.Business.Owner.Lock();};Assert.Throws<UnauthorizedAccessException>(()=>settings.Save(f.Business.Owner,Client()));
        Assert.Equal(watermark,Watermark(f));Assert.Null(settings.Load());var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);Assert.Null(files.Read(DeviceCredentialKind.GoogleClient));Assert.Empty(Directory.GetFiles(files.DirectoryPath,"*.partial"));
    }
    [Fact]
    public void FailureBeforeFilePublicationRollsBackConfigurationAndPreservesThePriorClient()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);var saved=settings.Save(f.Business.Owner,Client());long watermark=Watermark(f);
        settings.FailurePoint=point=>{if(point=="Configuration recorded")throw new IOException("Fictional private configuration diagnostic");};var error=Assert.Throws<GoogleConnectionFailure>(()=>settings.Save(f.Business.Owner,Client("failed"),saved.Id));Assert.DoesNotContain("Fictional",error.ToString());
        Assert.Equal(saved,settings.Current(f.Business.Owner));Assert.Equal(watermark,Watermark(f));Assert.Equal("Fictional-TEST-secret-first",settings.Load()!.Client.ClientSecret);
    }
    [Fact]
    public void InterruptedFileAndDatabasePublicationFailsClosedRatherThanMixingGenerations()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);var saved=settings.Save(f.Business.Owner,Client());long watermark=Watermark(f);
        settings.FailurePoint=point=>{if(point=="Protected file published")throw new IOException("Fictional unconfirmed commit");};Assert.Throws<GoogleConnectionFailure>(()=>settings.Save(f.Business.Owner,Client("uncertain"),saved.Id));
        Assert.Equal(saved,settings.Current(f.Business.Owner));Assert.Equal(watermark,Watermark(f));Assert.Equal("ConfigurationNeedsReview",Assert.Throws<GoogleConnectionFailure>(()=>settings.Load()).Code);
        settings.FailurePoint=null;var repaired=settings.Save(f.Business.Owner,Client("reviewed"),saved.Id);Assert.NotEqual(saved.Id,repaired.Id);Assert.Equal("Fictional-TEST-secret-reviewed",settings.Load()!.Client.ClientSecret);
    }
    [Fact]
    public async Task ConcurrentReviewedReplacementsAllowOneMatchingGenerationAndOneAudit()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var initial=new GoogleClientSettings(f.Business.Store,privateFiles.Folder).Save(f.Business.Owner,Client());long watermark=Watermark(f);
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(i=>Task.Run(()=>
        {
            try{new GoogleClientSettings(new BusinessStore(f.Business.Files.DatabasePath),privateFiles.Folder).Save(f.Business.Owner,Client("concurrent-"+i),initial.Id);return true;}catch(ArgumentException){return false;}
        })));
        Assert.Single(results,result=>result);Assert.Equal(watermark+1,Watermark(f));var loaded=new GoogleClientSettings(f.Business.Store,privateFiles.Folder).Load();Assert.NotNull(loaded);Assert.Equal(2,loaded.Configuration.Revision);
    }
    [Fact]
    public void AReviewedReplacementCanRepairDamagedProtectedClientBytes()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);var saved=settings.Save(f.Business.Owner,Client());
        var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);string path=Path.Combine(files.DirectoryPath,"GoogleClient.dat");byte[] damaged=File.ReadAllBytes(path);damaged[^1]^=1;File.WriteAllBytes(path,damaged);
        Assert.Throws<InvalidDataException>(()=>settings.Load());Assert.Equal(damaged,File.ReadAllBytes(path));Assert.Equal(saved,settings.Current(f.Business.Owner));
        var replacement=settings.Save(f.Business.Owner,Client("reviewed-repair"),saved.Id);Assert.NotEqual(saved.Id,replacement.Id);Assert.Equal("Fictional-TEST-secret-reviewed-repair",settings.Load()!.Client.ClientSecret);
    }

    [Fact]
    public void ReidentifiedStoresDoNotInheritAConfiguredClientOrAcceptOldHandles()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var settings=new GoogleClientSettings(f.Business.Store,privateFiles.Folder);settings.Save(f.Business.Owner,Client());
        f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");Assert.Throws<UnauthorizedAccessException>(()=>settings.Load());var reopened=new GoogleClientSettings(new BusinessStore(f.Business.Files.DatabasePath),privateFiles.Folder);Assert.Null(reopened.Load());
    }
}
