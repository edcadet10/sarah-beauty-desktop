using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Exports;

internal static class ReadablePurchaseTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var bills=new ReadableTableBuilder(source,budget,"Supplier bills","PurchaseId","BusinessId","Revision","Name","Archived","SupplierId","SupplierReference","ReliableReference","PurchaseDate","ServiceDate","DueDate","Status","ApprovedByOwnerId","ApprovedAtUtc","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var lines=new ReadableTableBuilder(source,budget,"Supplier bill lines","LineId","PurchaseId","BusinessId","Description","Quantity","UnitCostCents","NetCostCents","TaxPaidCents","BusinessUseFraction","GrossCents","BusinessCents","AccountId","AssetId","BookingId");ReadableRecords.ForEach<Purchase>(db,source,"Purchase",(value,audit)=>
        {
            bills.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.SupplierId,value.SupplierReference,value.ReliableReference,value.PurchaseDate,value.ServiceDate,value.DueDate,value.Status,value.ApprovedBy,value.ApprovedAtUtc,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            foreach(var line in value.Lines.OrderBy(l=>l.Id))lines.Add(line.Id,value.Id,source.BusinessId,line.Description,line.Quantity,line.UnitCostCents,Money.Multiply(line.Quantity,line.UnitCostCents),line.TaxPaidCents,line.BusinessUseFraction,line.GrossCents,line.BusinessCents,line.AccountId,line.AssetId,line.BookingId);
        });
        var credits=new ReadableTableBuilder(source,budget,"Supplier bill credits","CreditId","BusinessId","Revision","PurchaseId","Archived","SupplierReference","ReliableReference","Date","Status","ApprovedByOwnerId","ApprovedAtUtc","ReversesCreditId","Sign","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var reductions=new ReadableTableBuilder(source,budget,"Supplier credit line reductions","CreditId","BusinessId","PurchaseId","LineId","Cents","TaxCents","BusinessCents");ReadableRecords.ForEach<PurchaseCredit>(db,source,"PurchaseCredit",(value,audit)=>
        {
            credits.Add(value.Id,source.BusinessId,value.Revision,value.PurchaseId,value.Archived,value.SupplierReference,value.ReliableReference,value.Date,value.Status,value.ApprovedBy,value.ApprovedAtUtc,value.ReversesId,value.Sign,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            foreach(var line in value.Lines.OrderBy(l=>l.LineId))reductions.Add(value.Id,source.BusinessId,value.PurchaseId,line.LineId,line.Cents,line.TaxCents,line.BusinessCents);
        });
        var settlements=ReadableSqlTables.Read(db,source,budget,"Supplier settlements",["SettlementId","BusinessId","MovementId","PurchaseId","LineId","Date","Cents","BusinessCents","DrawCents","Sign","ReversesSettlementId","RefundsSettlementId","ConsumesSettlementId"],"SELECT S.Id,R.BusinessId,S.MovementId,S.PurchaseId,S.LineId,S.Date,S.Cents,S.BusinessCents,S.DrawCents,S.Sign,S.ReversesId,S.RefundsId,S.ConsumesId FROM PurchaseSettlements S JOIN Records R ON R.Id=S.PurchaseId WHERE R.BusinessId=$business ORDER BY S.Date,S.Id;");
        return new[]{bills.Freeze(),lines.Freeze(),credits.Freeze(),reductions.Freeze(),settlements};
    });
}
