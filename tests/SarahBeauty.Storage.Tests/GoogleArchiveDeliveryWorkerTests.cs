using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleArchiveDeliveryWorkerTests
{
    private static GoogleArchiveDeliveryWorker Worker(GoogleArchiveDeliveriesTests.Fixture f,HttpClient client)=>new(f.Google.Ledger.Business.Store,f.Google.Connections,f.Google.Settings,client);
    [Fact]
    public async Task RealEncryptedArchiveTraversesTheActualTransportAndPersistsVerifiedCoverageWithoutUnlocking()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create(ledger=>{ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));var invoice=ApplicationTests.Invoice(ledger,10000);new DocumentArtifacts(ledger.Business.Store).Render(ledger.Business.Owner,invoice.Id);new ManagedAttachments(ledger.Business.Store).Import(ledger.Business.Owner,ManagedAttachmentTests.Pdf(ledger.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());});using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);long watermark=f.Google.Watermark;f.Google.Ledger.Business.Owner.Lock();var expiry=f.Google.Ledger.Business.Owner.ExpiresAt;
        var completed=await Worker(f,client).DeliverCurrentAsync(f.Archive.Id);Assert.Equal("Succeeded",completed.Status);Assert.Equal(await File.ReadAllBytesAsync(f.Archive.Destination),server.Received);Assert.Equal(1,server.ContentWrites);Assert.Equal(watermark,f.Google.Watermark);Assert.Equal(expiry,f.Google.Ledger.Business.Owner.ExpiresAt);
        string received=Path.Combine(f.Google.Ledger.Business.Files.Folder,"fictional-received.age"),reviewRoot=Path.Combine(f.Google.Ledger.Business.Files.Folder,"fictional-received-review");await File.WriteAllBytesAsync(received,server.Received);Directory.CreateDirectory(reviewRoot);using(var review=await new ArchiveRestores().StageAsync(received,reviewRoot,f.Material.ProtectedIdentity,BusinessFixture.Phrase,f.Archive.Result)){Assert.True(review.MatchedRecordedArchive);Assert.Equal(1,review.Manifest.Facts.MovementCount);Assert.Equal(3,review.Manifest.Files.Length);Assert.Equal(watermark,review.Manifest.Watermark);}Assert.Equal(expiry,f.Google.Ledger.Business.Owner.ExpiresAt);
    }
    [Fact]
    public async Task EarlierUnconfirmedCompletionIsRetriedUsingTheSameFileWithoutAnotherContentWrite()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);var worker=Worker(f,client);worker.FailurePoint=point=>{if(point=="Remote archive verified")throw new GoogleConnectionFailure("CompletionUnconfirmed","Fictional unconfirmed local completion");};await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>worker.DeliverCurrentAsync(f.Archive.Id));Assert.Equal(1,server.ContentWrites);
        var before=Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner));Assert.Equal("Failed",Assert.Single(before.Attempts).Status);await Worker(f,client).DeliverCurrentAsync(f.Archive.Id);var after=Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner));Assert.Equal(before.Delivery,after.Delivery);Assert.Equal(2,after.Attempts.Length);Assert.Contains(after.Attempts,a=>a.Status=="Succeeded");Assert.Equal(1,server.ContentWrites);
    }
    [Fact]
    public async Task ChangedPermissionAfterTransferCannotRecordSuccessOrChangeCapturedCoverage()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);var worker=Worker(f,client);long captured=f.Archive.Result!.Manifest.Watermark;
        worker.FailurePoint=point=>{if(point!="Archive content transfer completed")return;f.Google.Ledger.Post(f.Google.Ledger.Draft(FinancialKind.Receipt,123));f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(false,false,f.Permit.Policy.Revision)).GetAwaiter().GetResult();};await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>worker.DeliverCurrentAsync(f.Archive.Id));var history=Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner));Assert.Equal(captured,history.Delivery.ArchiveWatermark);Assert.True(f.Google.Watermark>captured);Assert.Equal("Failed",Assert.Single(history.Attempts).Status);Assert.Equal(1,server.ContentWrites);
    }
    [Fact]
    public async Task ChangedLocalBytesAreRejectedBeforeAnyArchiveContentRequest()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();byte[] bytes=await File.ReadAllBytesAsync(f.Archive.Destination);bytes[^1]^=1;await File.WriteAllBytesAsync(f.Archive.Destination,bytes);using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);
        await Assert.ThrowsAsync<InvalidDataException>(()=>Worker(f,client).DeliverCurrentAsync(f.Archive.Id));Assert.Equal(0,server.ContentWrites);Assert.Empty(server.Received);Assert.Equal("ArchiveVerificationFailed",Assert.Single(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts).FailureCode);
    }
    [Fact]
    public async Task RemoteGroupAccessAfterUploadRetainsAnUnconfirmedAttempt()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);var worker=Worker(f,client);worker.FailurePoint=point=>{if(point=="Archive content transfer completed")server.AddGroup=true;};Assert.Equal("GoogleArchiveNotPrivate",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>worker.DeliverCurrentAsync(f.Archive.Id))).Code);Assert.Equal("Failed",Assert.Single(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts).Status);Assert.NotEmpty(server.Received);
    }
    [Fact]
    public async Task ExclusiveLeasePreventsAnotherWorkerAndEarlierRunningAttemptBecomesInterruptedOnRetry()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();var delivery=f.Reserve();f.Deliveries.Begin(f.Permit,delivery);using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);
        using(var held=GoogleArchiveDeliveryLease.TryAcquire(f.Google.Ledger.Business.Store)){Assert.NotNull(held);Assert.Equal("GoogleArchiveBusy",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>Worker(f,client).DeliverCurrentAsync(f.Archive.Id))).Code);Assert.Equal(0,server.Calls);}
        await Worker(f,client).DeliverCurrentAsync(f.Archive.Id);var history=Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner));Assert.Equal(2,history.Attempts.Length);Assert.Contains(history.Attempts,a=>a.Status=="Interrupted" && a.FailureCode=="CompletionUnconfirmed");Assert.Contains(history.Attempts,a=>a.Status=="Succeeded");
    }
    [Fact]
    public async Task CancellationAfterRemoteVerificationPreservesTheFileAndRecordsNoSuccess()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);using var cancellation=new CancellationTokenSource();var worker=Worker(f,client);worker.FailurePoint=point=>{if(point=="Remote archive verified")cancellation.Cancel();};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>worker.DeliverCurrentAsync(f.Archive.Id,cancellation.Token));Assert.Equal("Cancelled",Assert.Single(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts).Status);Assert.NotEmpty(server.Received);
    }
    [Fact]
    public async Task CancellationAfterTheEarlierCheckButBeforeWriterReservationCannotRecordSuccess()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();using var server=new GoogleArchiveTestServer(f.Permit.Policy);using var client=new HttpClient(server);using var cancellation=new CancellationTokenSource();var worker=Worker(f,client);var store=f.Google.Ledger.Business.Store;
        worker.FailurePoint=point=>{if(point=="Remote archive verified")store.BeforeTransactionReservation=()=>{store.BeforeTransactionReservation=null;cancellation.Cancel();};};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>worker.DeliverCurrentAsync(f.Archive.Id,cancellation.Token));Assert.Equal("Cancelled",Assert.Single(Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner)).Attempts).Status);Assert.NotEmpty(server.Received);
    }
}
