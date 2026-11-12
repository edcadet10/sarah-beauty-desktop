using System.Net;
using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleArchiveQueueTests
{
    private sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private static GoogleArchiveQueue Queue(GoogleArchiveDeliveriesTests.Fixture f,HttpClient client,TimeProvider time,GoogleArchiveDeliveryWorker? worker=null)=>new(f.Google.Ledger.Business.Store,f.Google.Settings,worker??new(f.Google.Ledger.Business.Store,f.Google.Connections,f.Google.Settings,client),time);
    [Fact]
    public async Task RestartCoalescesCoveredArchivesWithoutRepeatedUploadsOrHumanSessionRenewal()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);var clock=new Clock();long watermark=f.Google.Watermark;f.Google.Ledger.Business.Owner.Lock();var expiry=f.Google.Ledger.Business.Owner.ExpiresAt;var first=await Queue(f,client,clock).RunDueAsync();Assert.Equal("Succeeded",first.Status);Assert.Equal(watermark,first.State!.DeliveredWatermark);int calls=server.Calls;var restarted=await Queue(f,client,clock).RunDueAsync();Assert.Equal("Succeeded",restarted.Status);Assert.Equal(calls,server.Calls);Assert.Equal(1,server.ContentWrites);Assert.Equal(watermark,f.Google.Watermark);Assert.Equal(expiry,f.Google.Ledger.Business.Owner.ExpiresAt);
    }
    [Theory]
    [InlineData(false,"GoogleUnavailable")]
    [InlineData(true,"GoogleQuotaLimited")]
    public async Task OfflineAndQuotaBackoffPersistAcrossRestartWithoutPrivateDetails(bool quota,string code)
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy){Offline=!quota,ErrorStatus=quota?HttpStatusCode.TooManyRequests:null};using var client=new HttpClient(server);var clock=new Clock();var queue=Queue(f,client,clock);await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>queue.RunDueAsync());var failed=queue.Current(f.Google.Ledger.Business.Owner)!;Assert.Equal("Failed",failed.Status);Assert.Equal(1,failed.FailureCount);Assert.Equal(code,failed.FailureCode);Assert.DoesNotContain("Fictional private",JsonSerializer.Serialize(failed));int calls=server.Calls;
        clock.Now=clock.Now.AddSeconds(30);Assert.Equal("Failed",(await Queue(f,client,clock).RunDueAsync()).Status);Assert.Equal(calls,server.Calls);clock.Now=clock.Now.AddSeconds(31);server.Offline=false;server.ErrorStatus=null;var completed=await Queue(f,client,clock).RunDueAsync();Assert.Equal("Succeeded",completed.Status);Assert.Equal(0,completed.State!.FailureCount);Assert.Null(completed.State.NextAttemptAtUtc);
    }
    [Fact]
    public async Task LaterChangesWaitForANewLocalArchiveWithoutUploadingOlderCoverageAgain()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);var worker=new GoogleArchiveDeliveryWorker(f.Google.Ledger.Business.Store,f.Google.Connections,f.Google.Settings,client){FailurePoint=point=>{if(point=="Archive content transfer completed")f.Google.Ledger.Post(f.Google.Ledger.Draft(FinancialKind.Receipt,200));}};var clock=new Clock();var result=await Queue(f,client,clock,worker).RunDueAsync();Assert.Equal("Waiting for local archive",result.Status);Assert.Equal(f.Archive.Result!.Manifest.Watermark,result.State!.DeliveredWatermark);Assert.True(result.State.ObservedWatermark>result.State.DeliveredWatermark);int calls=server.Calls;Assert.Equal("Waiting for local archive",(await Queue(f,client,clock).RunDueAsync()).Status);Assert.Equal(calls,server.Calls);Assert.Equal(1,server.ContentWrites);
    }
    [Fact]
    public async Task QueueLeaseAndWithdrawnPermissionPreventRequestsAndCopiedSourcesAreNotEligible()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);var queue=Queue(f,client,new Clock());using(var lease=GoogleArchiveDeliveryLease.TryQueue(f.Google.Ledger.Business.Store)){Assert.NotNull(lease);Assert.Equal("Busy",(await queue.RunDueAsync()).Status);}Assert.Equal(0,server.Calls);
        f.Google.Ledger.Business.Store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(f.Archive with{InstanceId=Guid.NewGuid().ToString("N")})),("$key",BackupJobRecords.Key(f.Archive.Id))));Assert.Equal("Waiting for local archive",(await queue.RunDueAsync()).Status);Assert.Equal(0,server.Calls);await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(false,false,f.Permit.Policy.Revision));Assert.Equal("Not authorized",(await queue.RunDueAsync()).Status);Assert.Equal(0,server.Calls);
    }
}
