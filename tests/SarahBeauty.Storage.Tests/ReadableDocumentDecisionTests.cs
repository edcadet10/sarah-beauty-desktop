using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableDocumentDecisionTests
{
    [Fact]
    public void CapturedDecisionsStayFrozenAndExcludePrivateReasonAndEvidence()
    {
        using var f=new LedgerFixture();var quote=QuoteTerminalDecisionTests.Issue(f.Business);var invoice=ApplicationTests.Invoice(f,5000);var attachments=new ManagedAttachments(f.Business.Store);var evidence=attachments.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional-private-decision-file.pdf"),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());const string reason="PRIVATE fictional decision reason =SUM(1,2)";
        var first=new DocumentDecisions(f.Business.Store).RecordQuote(f.Business.Owner,QuoteTerminalDecisionTests.Draft(quote,DocumentDecisionAction.DeclineQuote) with{Reason=reason,Evidence=ManagedEvidenceFile.From(evidence)},BusinessFixture.Phrase,Guid.NewGuid().ToString());
        using var captured=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var before=ReadableDocumentDecisionTables.Build(captured,new());
        InvoiceVoidTests.Book(f).VoidInvoice(f.Business.Owner,InvoiceVoidTests.Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString());f.Business.Owner.Lock();var after=ReadableDocumentDecisionTables.Build(captured,new());
        Assert.Equal(before.Select(t=>t.Sha256),after.Select(t=>t.Sha256));var row=Assert.Single(before[0].Rows);Assert.Equal(first.Id,row[0]);Assert.Equal(first.VersionId,row[3]);Assert.Equal(first.RequestId,row[4]);Assert.Equal("DeclineQuote",row[5]);Assert.Equal("Issued",row[7]);Assert.Equal("Declined",row[8]);Assert.Equal("1",Assert.Single(before[1].Rows)[1]);
        string json=JsonSerializer.Serialize(before);foreach(string privateValue in new[]{reason,evidence.Version.Id,evidence.Version.Sha256,evidence.SnapshotHash,evidence.Version.OriginalFileName,evidence.Version.RelativePath,f.Business.Files.Folder})Assert.DoesNotContain(privateValue,json);
        Assert.DoesNotContain(before[0].Columns,c=>c.Contains("Reason",StringComparison.Ordinal)||c.Contains("Evidence",StringComparison.Ordinal)||c.Contains("Hash",StringComparison.Ordinal)||c.Contains("Snapshot",StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyHistoryDoesNotInventAStatusAndCalendarDatesRemainGregorian()
    {
        using var f=new BusinessFixture();var old=CultureInfo.CurrentCulture;try
        {
            CultureInfo.CurrentCulture=new("th-TH");using(var empty=new ReadableExportSnapshots(f.Store).Capture(f.Owner)){var tables=ReadableDocumentDecisionTables.Build(empty,new());Assert.Empty(tables[0].Rows);Assert.Equal("0",Assert.Single(tables[1].Rows)[1]);}
            var version=QuoteTerminalDecisionTests.Issue(f);var draft=QuoteTerminalDecisionTests.Draft(version,DocumentDecisionAction.WithdrawQuote) with{DecisionDate=DateOnly.FromDateTime(DateTime.Today)};new DocumentDecisions(f.Store).RecordQuote(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString());using var captured=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var row=Assert.Single(ReadableDocumentDecisionTables.Build(captured,new())[0].Rows);Assert.Equal(draft.DecisionDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),row[6]);Assert.Equal("Withdrawn",row[8]);
        }
        finally{CultureInfo.CurrentCulture=old;}
    }
}
