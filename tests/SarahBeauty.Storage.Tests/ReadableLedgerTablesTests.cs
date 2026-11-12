using System.Numerics;
using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableLedgerTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void ApprovedGrossTaxFeesAndBalancedJournalSidesRemainSeparateWithoutAnotherPosting()
    {
        using var f=new LedgerFixture();var receipt=f.Post(f.Draft(FinancialKind.Receipt,10000) with{TaxCents=1000,FeeCents=300,PaymentMethod="Card"});f.Post(f.Draft(FinancialKind.Expense,2500));long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableLedgerTables.Build(source,new());Assert.Equal(5,tables.Length);var posted=tables.Single(t=>t.Name=="Posted movements");Assert.Equal(2,posted.Rows.Count);var row=posted.Rows.Single(r=>Cell(posted,r,"MovementId")==receipt.Id);Assert.Equal("10000",Cell(posted,row,"GrossCents"));Assert.Equal("1000",Cell(posted,row,"TaxCents"));Assert.Equal("300",Cell(posted,row,"FeeCents"));Assert.Equal(f.Business.Owner.OwnerId,Cell(posted,row,"ApprovedByOwnerId"));var journal=tables.Single(t=>t.Name=="Journal lines");BigInteger debits=0,credits=0;foreach(var line in journal.Rows){debits+=BigInteger.Parse(Cell(journal,line,"DebitCents"));credits+=BigInteger.Parse(Cell(journal,line,"CreditCents"));}Assert.Equal(new BigInteger(12500),debits);Assert.Equal(debits,credits);Assert.Contains(journal.Rows,r=>Cell(journal,r,"MovementId")==receipt.Id && Cell(journal,r,"AccountId")==f.Accounts["1010"].Id && Cell(journal,r,"DebitCents")=="9700");Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void IncompleteDraftDatesAndTaxStayUnknownAndEvidenceInputsAreNotReadableCells()
    {
        using var f=new LedgerFixture();const string secret="Fictional-private-evidence-input";var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,0) with{Date=null,TaxResolved=false,EvidenceId=secret,Name="Fictional incomplete expense"});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableLedgerTables.Build(source,new());Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));Assert.Empty(tables.Single(t=>t.Name=="Posted movements").Rows);Assert.Empty(tables.Single(t=>t.Name=="Journal lines").Rows);var drafts=tables.Single(t=>t.Name=="Financial draft records");var row=drafts.Rows.Single(r=>r[0]==draft.Id);Assert.Equal("Draft",Cell(drafts,row,"Status"));Assert.Equal("",Cell(drafts,row,"Date"));Assert.Equal("",Cell(drafts,row,"TaxCents"));Assert.Equal("false",Cell(drafts,row,"TaxResolved"));
    }
    [Fact]
    public void ApprovedReversalKeepsTheOriginalIdentityAndOppositeJournalSides()
    {
        using var f=new LedgerFixture();var receipt=f.Post(f.Draft(FinancialKind.Receipt,10000));var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=receipt.Id});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableLedgerTables.Build(source,new());var posted=tables.Single(t=>t.Name=="Posted movements");var row=posted.Rows.Single(r=>Cell(posted,r,"MovementId")==reversal.Id);Assert.Equal(receipt.Id,Cell(posted,row,"ReversesMovementId"));Assert.Equal("Reversal",Cell(posted,row,"Kind"));Assert.Equal(2,posted.Rows.Count);var journal=tables.Single(t=>t.Name=="Journal lines");Assert.Contains(journal.Rows,r=>Cell(journal,r,"MovementId")==reversal.Id && Cell(journal,r,"AccountId")==f.Accounts["1010"].Id && Cell(journal,r,"CreditCents")=="10000");
    }
}
