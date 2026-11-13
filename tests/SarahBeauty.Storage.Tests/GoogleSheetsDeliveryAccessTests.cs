using System.Net;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsDeliveryAccessTests
{
    [Fact]
    public async Task FreshTokensAndIndependentPrivateChecksDoNotRenewLockedOwnerOrWatermark()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var attempt=new GoogleSheetsDeliveries(store).Begin(permit,job);
        var creation=new GoogleSheetsCreationCheckpoints(store);creation.RecordFile(job,creation.BeginCreate(permit,job,creation.Prepare(permit,job)),"fictional-created-workbook");int calls=0,waits=0;
        using var client=new HttpClient(new Handler(request=>
        {
            calls++;Assert.NotNull(request.Headers.Authorization);var facts=request.RequestUri!.AbsolutePath.EndsWith("/"+job.FolderId,StringComparison.Ordinal)?GoogleSheetsWorkbookCreationTests.FolderFacts(job,permit):GoogleSheetsWorkbookCreationTests.FileFacts(job,permit);
            return new(HttpStatusCode.OK){Content=new StringContent(facts.ToJsonString())};
        }));var access=new GoogleSheetsDeliveryAccess(store,f.Connections,permit,job,attempt,client){Wait=token=>{token.ThrowIfCancellationRequested();waits++;return Task.CompletedTask;}};
        long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();Assert.False(string.IsNullOrEmpty(await access.TokenAsync(default)));await access.PrivateAsync("staging",default);
        Assert.Equal(2,calls);Assert.Equal(3,waits);Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);
    }
    [Theory]
    [InlineData("permission")]
    [InlineData("attempt")]
    [InlineData("cancel")]
    public async Task ChangesDuringPacingPreventTheNextTokenAndHttp(string change)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var deliveries=new GoogleSheetsDeliveries(store);var attempt=deliveries.Begin(permit,job);int calls=0;
        using var client=new HttpClient(new Handler(_=>{calls++;throw new InvalidOperationException("No HTTP expected");}));using var cancellation=new CancellationTokenSource();var access=new GoogleSheetsDeliveryAccess(store,f.Connections,permit,job,attempt,client)
        {
            Wait=async token=>{if(change=="permission")await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision),token);else if(change=="attempt")deliveries.Fail(attempt,new IOException());else cancellation.Cancel();}
        };
        await Assert.ThrowsAnyAsync<Exception>(()=>access.FolderAsync(cancellation.Token));Assert.Equal(0,calls);
    }
    [Fact]
    public void SeparateStoreHandlesCannotHoldTheSameSheetsLeaseAndArchiveWorkUsesAnotherLease()
    {
        using var f=new BusinessFixture();var reopened=new BusinessStore(f.Store.DatabasePath);
        using(var first=GoogleSheetsDeliveryLease.TryAcquire(f.Store)){Assert.NotNull(first);Assert.Null(GoogleSheetsDeliveryLease.TryAcquire(reopened));using var archive=GoogleArchiveDeliveryLease.TryAcquire(f.Store);Assert.NotNull(archive);}
        using var next=GoogleSheetsDeliveryLease.TryAcquire(reopened);Assert.NotNull(next);
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return Task.FromResult(respond(request));}
    }
}
