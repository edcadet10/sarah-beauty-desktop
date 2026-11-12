using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableSupplierReportTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void PaidBillCreditKeepsGrossAndBusinessCreditSeparateFromPayableAndLaterRefund()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var credits=new PurchaseCreditBook(f.Business.Store);
        const string reason="Fictional-private-supplier-report-credit";
        var draft=credits.SaveDraft(f.Business.Owner,new(){Name=reason,PurchaseId=bill.Id,Date=f.Date,Lines=[new(bill.Lines[0].Id,1100,100)]});
        credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.PurchaseRefund,1100) with{OriginalId=payment.Id,SupplierRefundPortions=[new(bill.Lines[0].Id,1100)]});
        f.Business.Owner.Lock();
        var tables=ReadableSupplierReportTables.Build(source,new(),f.Date);
        Assert.Equal(4,tables.Length);Assert.DoesNotContain(reason,JsonSerializer.Serialize(tables));
        var suppliers=tables.Single(t=>t.Name=="Report supplier balances");
        var supplier=Assert.Single(suppliers.Rows);
        Assert.Equal("0",Cell(suppliers,supplier,"PayableCents"));
        Assert.Equal("1100",Cell(suppliers,supplier,"SupplierCreditCents"));
        var bills=tables.Single(t=>t.Name=="Report supplier bill balances");
        Assert.Equal("-1100",Cell(bills,Assert.Single(bills.Rows),"NetCents"));
        var lines=tables.Single(t=>t.Name=="Report supplier bill line balances");
        var line=lines.Rows.Single(r=>Cell(lines,r,"LineId")==bill.Lines[0].Id);
        Assert.Equal("-1100",Cell(lines,line,"NetGrossCents"));
        Assert.Equal("-550",Cell(lines,line,"NetBusinessCents"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void ClosedPeriodKeepsOriginalSupplierAndBillBalancesAfterReviewedAdjustment()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        var supplier=new BusinessResources(f.Business.Store).SaveSupplier(f.Business.Owner,new(){Name="Fictional retained supplier"});
        var purchases=new PurchaseBook(f.Business.Store);
        var bill=purchases.SaveDraft(f.Business.Owner,new(){Name="Fictional retained supplier bill",SupplierId=supplier.Id,PurchaseDate=f.Date,Lines=[new(Guid.NewGuid().ToString(),"Fictional service",1,5000,0,1m,f.Accounts["5010"].Id)]});
        bill=purchases.Approve(f.Business.Owner,bill.Id,bill.Revision,BusinessFixture.Phrase);
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,"Fictional reviewed supplier adjustment",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var credits=new PurchaseCreditBook(f.Business.Store);
        var credit=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional actual supplier adjustment",PurchaseId=bill.Id,Date=f.Date,Lines=[new(bill.Lines[0].Id,1000,0)]});
        credits.Approve(f.Business.Owner,credit.Id,credit.Revision,BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableSupplierReportTables.Build(source,new(),new(2027,1,31));
        var bills=tables.Single(t=>t.Name=="Report supplier bill balances");
        Assert.Equal("5000",Cell(bills,bills.Rows.Single(r=>r[0]==closed.Id),"PayableCents"));
        Assert.Equal("4000",Cell(bills,bills.Rows.Single(r=>r[0]==source.Source.GenerationId),"PayableCents"));
        Assert.All(bills.Rows,r=>Assert.Equal("",Cell(bills,r,"DueDate")));
        var coverage=tables.Single(t=>t.Name=="Supplier report detail coverage");
        Assert.Equal("Not retained",Cell(coverage,coverage.Rows.Single(r=>r[0]==closed.Id),"BillLineBalances"));
        Assert.DoesNotContain(tables.Single(t=>t.Name=="Report supplier bill line balances").Rows,r=>r[0]==closed.Id);
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }
}
