using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Ownership;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnershipWorkbookDeliveryTests
{
    [Fact]
    public async Task ConfirmedSharesAreActuallyWrittenAndReadBackBeforePublication()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var business=f.Ledger.Business;f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));
        var confirmed=new OwnerInterests(business.Store).Confirm(business.Owner,OwnerInterestConfirmationTests.Draft(business,0,new(2027,1,1),.6m) with{Reason="PRIVATE fictional ownership decision"});
        var folder=await f.Create();await f.Settings.SaveAsync(business.Owner,new(false,true,folder.Revision));var permit=f.Settings.Authorize(GoogleOutputKind.SheetsExport);
        using var server=new GoogleSheetsTestServer(business.Store,permit);using var client=new HttpClient(server);long watermark=f.Watermark;business.Owner.Lock();
        var worker=new GoogleSheetsDeliveryWorker(business.Store,f.Connections,f.Settings,client){WaitForRequest=token=>{token.ThrowIfCancellationRequested();return Task.CompletedTask;}};
        var completed=await worker.ExportCurrentAsync(new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31)));
        Assert.Equal("Succeeded",completed.Status);Assert.True(server.Published);Assert.Equal(confirmed.Id,server.Cells[("Ownership confirmations",1,0)]);Assert.Equal("Confirmed",server.Cells[("Ownership coverage",1,2)]);Assert.Equal(confirmed.Id,server.Cells[("Ownership coverage",1,3)]);
        foreach(var share in confirmed.Shares)
        {
            int row=Enumerable.Range(1,2).Single(r=>server.Cells[("Ownership confirmed shares",r,2)]==share.OwnerId);
            Assert.Equal(share.Fraction.ToString(System.Globalization.CultureInfo.InvariantCulture),server.Cells[("Ownership confirmed shares",row,4)]);
        }
        Assert.DoesNotContain(server.Cells.Values,cell=>cell.Contains("PRIVATE fictional ownership decision",StringComparison.Ordinal));Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,business.Owner.ExpiresAt);
        Assert.Equal(12345L,TestStore.Scalar(business.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(1,server.Creates);Assert.Equal(1,server.PublicationWrites);
    }
}
