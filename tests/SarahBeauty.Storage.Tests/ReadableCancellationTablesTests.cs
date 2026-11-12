using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableCancellationTablesTests
{
    private const string Private="Fictional-private-cancellation-agreement-evidence";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void MixedApprovedDecisionRetainsExactChoicesActionsAndBalancedActualRefundJournal()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        var application=CancellationSourceTests.Apply(f,receipt.Id,invoice,11000);
        var book=new CancellationCases(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,CancellationPreviewTests.Draft(f,invoice) with
        {
            Name=Private,AgreementReview=Private,EvidenceId=Private,ClientReason=Private,
            Unapplications=[new(application.Id,11000)],
            Reductions=[new(invoice.Id,[new(invoice.Snapshot.Lines[0].ChargeId,1)])],
            Values=[CancellationPreviewTests.Value(f,receipt.Id,retain:2200,refund:3300,tax:300,transfer:5500) with{RefundEvidenceId=Private}]
        });
        var saved=book.Approve(f.Business.Owner,book.Preview(f.Business.Owner,draft),BusinessFixture.Phrase);
        var booking=new BookingBook(f.Business.Store).List(f.Business.Owner).Single();
        new BookingBook(f.Business.Store).Save(f.Business.Owner,booking with{Name="Fictional later renamed cancelled booking"});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableCancellationTables.Build(source,new());
        Assert.Equal(8,tables.Length);
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var choices=tables.Single(t=>t.Name=="Cancellation value choices");
        Assert.Equal(2,choices.Rows.Count);
        var approved=choices.Rows.Single(r=>r[0]=="ApprovedDecision");
        Assert.Equal(saved.Id,Cell(choices,approved,"SourceId"));
        Assert.Equal("2200",Cell(choices,approved,"RetainCents"));
        Assert.Equal("3300",Cell(choices,approved,"RefundCents"));
        Assert.Equal("5500",Cell(choices,approved,"TransferCents"));
        var actions=tables.Single(t=>t.Name=="Cancellation approved actions");
        Assert.Equal(saved.Snapshot.Actions.Length,actions.Rows.Count);
        Assert.Single(actions.Rows,r=>Cell(actions,r,"Kind")=="Refund");
        var observed=tables.Single(t=>t.Name=="Cancellation frozen booking facts");
        Assert.Equal("Cancelled",Cell(observed,observed.Rows.Single(r=>Cell(observed,r,"Phase")=="After"),"BookingStatus"));
        Assert.DoesNotContain("Fictional later renamed",JsonSerializer.Serialize(observed));
        var journal=tables.Single(t=>t.Name=="Cancellation resulting journal");
        Assert.Equal(3300,journal.Rows.Sum(r=>long.Parse(Cell(journal,r,"DebitCents"))));
        Assert.Equal(3300,journal.Rows.Sum(r=>long.Parse(Cell(journal,r,"CreditCents"))));
        Assert.Equal(7700,f.Balance("1010"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void IncompleteDraftDatesRemainUnknownAndStatusOnlyApprovalInventsNoRefundOrJournal()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f);
        var book=new CancellationCases(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,CancellationPreviewTests.Draft(f,invoice) with{Name=Private});
        book.Approve(f.Business.Owner,book.Preview(f.Business.Owner,draft),BusinessFixture.Phrase);
        var incomplete=book.SaveDraft(f.Business.Owner,new(){Name=Private,BookingId=invoice.BookingId});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableCancellationTables.Build(source,new());
        var drafts=tables.Single(t=>t.Name=="Cancellation draft records");
        var row=drafts.Rows.Single(r=>r[0]==incomplete.Id);
        Assert.Equal("",Cell(drafts,row,"RequestedDate"));
        Assert.Equal("",Cell(drafts,row,"DecisionDate"));
        Assert.Empty(tables.Single(t=>t.Name=="Cancellation approved actions").Rows);
        Assert.Empty(tables.Single(t=>t.Name=="Cancellation resulting journal").Rows);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
}
