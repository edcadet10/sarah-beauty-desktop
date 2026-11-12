using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableSupplierReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;string current=source.GenerationId;
        var suppliers=new ReadableTableBuilder(source,budget,"Report supplier balances","ReportId","BusinessId","SupplierId","NameAtCapture","PayableCents","OverdueCents","DueTodayCents","FutureCents","UnknownDueCents","SupplierCreditCents");
        var bills=new ReadableTableBuilder(source,budget,"Report supplier bill balances","ReportId","BusinessId","PurchaseId","SupplierId","BillNameAtCapture","PurchaseDate","DueDate","NetCents","PayableCents","SupplierCreditCents");
        var lines=new ReadableTableBuilder(source,budget,"Report supplier bill line balances","ReportId","BusinessId","PurchaseId","LineId","NetGrossCents","NetBusinessCents");
        var coverage=new ReadableTableBuilder(source,budget,"Supplier report detail coverage","ReportId","BusinessId","SupplierBalances","BillBalances","BillLineBalances");
        var due=PurchasePayments.Outstanding(db,source.BusinessId,through);
        foreach(var row in SupplierPositions.Read(db,source.BusinessId,through,due).OrderBy(r=>r.SupplierId))AddSupplier(suppliers,current,source.BusinessId,row);
        var purchases=RecordStore<Purchase>.ReadBusiness(db,source.BusinessId,"Purchase").ToDictionary(p=>p.Id);
        foreach(var row in due.OrderBy(r=>r.PurchaseId))
        {
            AddBill(bills,current,source.BusinessId,row);
            foreach(var line in PurchaseBalances.Lines(db,source.BusinessId,purchases[row.PurchaseId],through).OrderBy(l=>l.LineId))lines.Add(current,source.BusinessId,row.PurchaseId,line.LineId,line.Gross,line.Business);
        }
        coverage.Add(current,source.BusinessId,"Calculated from captured source","Calculated from captured source","Calculated from captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>
        {
            foreach(var row in period.Reports.Suppliers.OrderBy(r=>r.SupplierId))AddSupplier(suppliers,id,source.BusinessId,row);
            foreach(var row in period.Reports.SupplierBills.OrderBy(r=>r.PurchaseId))AddBill(bills,id,source.BusinessId,row);
            coverage.Add(id,source.BusinessId,"As recorded at close","As recorded at close","Not retained");
        });
        return new[]{suppliers.Freeze(),bills.Freeze(),lines.Freeze(),coverage.Freeze()};
    });

    private static void AddSupplier(ReadableTableBuilder table,string report,string business,SupplierPosition row)=>table.Add(report,business,row.SupplierId,row.Name,row.OpenCents,row.OverdueCents,row.DueTodayCents,row.FutureCents,row.UnknownDueCents,row.CreditCents);
    private static void AddBill(ReadableTableBuilder table,string report,string business,PurchaseDue row)=>table.Add(report,business,row.PurchaseId,row.SupplierId,row.Name,row.PurchaseDate,row.DueDate,row.Cents,row.PayableCents,row.CreditCents);
}
