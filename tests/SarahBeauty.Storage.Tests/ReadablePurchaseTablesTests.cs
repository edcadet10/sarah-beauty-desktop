using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadablePurchaseTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private static Purchase Bill(LedgerFixture f,long? tax=1000,decimal? businessUse=0.5m)
    {
        var supplier=new BusinessResources(f.Business.Store).SaveSupplier(f.Business.Owner,new(){Name="Fictional readable bill supplier"});return new PurchaseBook(f.Business.Store).SaveDraft(f.Business.Owner,new(){Name="Fictional readable supplier bill",SupplierId=supplier.Id,PurchaseDate=f.Date,EvidenceId="Fictional-private-purchase-evidence",Lines=[new(Guid.NewGuid().ToString(),"Fictional purchased service",2,5000,tax,businessUse,f.Accounts["5010"].Id)]});
    }
    [Fact]
    public void DraftTaxAndUseStayUnknownAndApprovedBillsRetainLineFactsWithoutPostingExpense()
    {
        using var f=new LedgerFixture();var incomplete=Bill(f,null,null);var bill=Bill(f);var approved=new PurchaseBook(f.Business.Store).Approve(f.Business.Owner,bill.Id,bill.Revision,BusinessFixture.Phrase);using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadablePurchaseTables.Build(source,new());Assert.Equal(5,tables.Length);Assert.DoesNotContain("Fictional-private",JsonSerializer.Serialize(tables));var parents=tables.Single(t=>t.Name=="Supplier bills");Assert.Equal("Draft",Cell(parents,parents.Rows.Single(r=>r[0]==incomplete.Id),"Status"));Assert.Equal("Approved",Cell(parents,parents.Rows.Single(r=>r[0]==approved.Id),"Status"));var lines=tables.Single(t=>t.Name=="Supplier bill lines");var unknown=lines.Rows.Single(r=>r[1]==incomplete.Id);Assert.Equal("",Cell(lines,unknown,"TaxPaidCents"));Assert.Equal("",Cell(lines,unknown,"BusinessUseFraction"));Assert.Equal("",Cell(lines,unknown,"GrossCents"));var known=lines.Rows.Single(r=>r[1]==approved.Id);Assert.Equal("10000",Cell(lines,known,"NetCostCents"));Assert.Equal("11000",Cell(lines,known,"GrossCents"));Assert.Equal("5500",Cell(lines,known,"BusinessCents"));Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void ActualSupplierCreditAndItsReversalRetainSignedSourceReductionsWithoutPrivateReasons()
    {
        using var f=new LedgerFixture();var bill=Bill(f);bill=new PurchaseBook(f.Business.Store).Approve(f.Business.Owner,bill.Id,bill.Revision,BusinessFixture.Phrase);const string secret="Fictional-private-supplier-credit-reason";var book=new PurchaseCreditBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name=secret,PurchaseId=bill.Id,Date=f.Date,Lines=[new(bill.Lines[0].Id,5500,500)],EvidenceId=secret});var credit=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);var undo=book.SaveDraft(f.Business.Owner,book.ReversalDraft(f.Business.Owner,credit.Id) with{Name=secret,Date=f.Date.AddDays(1),EvidenceId=secret});var reversed=book.Approve(f.Business.Owner,undo.Id,undo.Revision,BusinessFixture.Phrase);using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadablePurchaseTables.Build(source,new());Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var credits=tables.Single(t=>t.Name=="Supplier bill credits");Assert.Equal(2,credits.Rows.Count);var row=credits.Rows.Single(r=>r[0]==reversed.Id);Assert.Equal(credit.Id,Cell(credits,row,"ReversesCreditId"));Assert.Equal("-1",Cell(credits,row,"Sign"));var lines=tables.Single(t=>t.Name=="Supplier credit line reductions");Assert.Equal("5500",Cell(lines,lines.Rows.First(),"Cents"));Assert.Equal("2750",Cell(lines,lines.Rows.First(),"BusinessCents"));Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void ActualMixedBusinessCashPaymentRetainsBusinessAndOwnerDrawPortionsWithOneCashMovement()
    {
        using var f=new LedgerFixture();var bill=Bill(f,0,0.5m);bill=new PurchaseBook(f.Business.Store).Approve(f.Business.Owner,bill.Id,bill.Revision,BusinessFixture.Phrase);var payment=f.Post(f.Draft(FinancialKind.PurchasePayment,10000) with{PurchaseId=bill.Id,EconomicOwnerId=f.Business.Owner.OwnerId});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var table=ReadablePurchaseTables.Build(source,new()).Single(t=>t.Name=="Supplier settlements");var row=Assert.Single(table.Rows);Assert.Equal(payment.Id,Cell(table,row,"MovementId"));Assert.Equal("10000",Cell(table,row,"Cents"));Assert.Equal("5000",Cell(table,row,"BusinessCents"));Assert.Equal("5000",Cell(table,row,"DrawCents"));Assert.Equal("1",Cell(table,row,"Sign"));Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
}
