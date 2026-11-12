using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentDecisionWorkbookTests
{
    [Fact]
    public async Task ActualPostedDecisionCellsAreReadBackBeforePublicationWithoutPrivateReasons()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var business=f.Ledger.Business;f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));var quote=QuoteTerminalDecisionTests.Issue(business);const string reason="PRIVATE fictional retained decision reason";var declined=new DocumentDecisions(business.Store).RecordQuote(business.Owner,QuoteTerminalDecisionTests.Draft(quote,DocumentDecisionAction.DeclineQuote) with{Reason=reason},BusinessFixture.Phrase,Guid.NewGuid().ToString());var invoice=ApplicationTests.Invoice(f.Ledger,5000,.09m);var voided=InvoiceVoidTests.Book(f.Ledger).VoidInvoice(business.Owner,InvoiceVoidTests.Draft(invoice) with{Reason=reason},BusinessFixture.Phrase,Guid.NewGuid().ToString());
        var folder=await f.Create();await f.Settings.SaveAsync(business.Owner,new(false,true,folder.Revision));var permit=f.Settings.Authorize(GoogleOutputKind.SheetsExport);using var server=new GoogleSheetsTestServer(business.Store,permit);using var client=new HttpClient(server);long watermark=f.Watermark;business.Owner.Lock();var worker=new GoogleSheetsDeliveryWorker(business.Store,f.Connections,f.Settings,client){WaitForRequest=token=>{token.ThrowIfCancellationRequested();return Task.CompletedTask;}};
        var completed=await worker.ExportCurrentAsync(DocumentDecisionExportContractTests.Selection);Assert.Equal("Succeeded",completed.Status);Assert.True(server.Published);Assert.Equal(38,new GoogleSheetsHealthService(business.Store).Current(DocumentDecisionExportContractTests.Selection).MatchingSuccess!.Job.SchemaVersion);Assert.Equal("2",server.Cells[("Document decision coverage",1,1)]);
        foreach(var decision in new[]{declined,voided})
        {
            int row=Enumerable.Range(1,2).Single(r=>server.Cells[("Document decisions",r,0)]==decision.Id);Assert.Equal(decision.VersionId,server.Cells[("Document decisions",row,3)]);Assert.Equal(decision.Action.ToString(),server.Cells[("Document decisions",row,5)]);Assert.Equal(decision.ResultStatus,server.Cells[("Document decisions",row,8)]);
        }
        Assert.DoesNotContain(server.Cells.Values,c=>c.Contains(reason,StringComparison.Ordinal));Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,business.Owner.ExpiresAt);Assert.Equal(1,server.Creates);Assert.Equal(1,server.PublicationWrites);Assert.Equal(12345L,TestStore.Scalar(business.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(business.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));
    }
}
