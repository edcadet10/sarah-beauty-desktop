using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class SupplierPositions
{
    internal static List<SupplierPosition> Read(DbWork db,string businessId,DateOnly through,List<PurchaseDue> bills)
    {
        var suppliers=RecordStore<Supplier>.ReadBusiness(db,businessId,"Supplier");
        return bills.GroupBy(b=>b.SupplierId).Select(group=>new SupplierPosition(group.Key,suppliers.Single(s=>s.Id==group.Key).Name,
            group.Sum(b=>b.PayableCents),group.Where(b=>b.DueDate<through).Sum(b=>b.PayableCents),group.Where(b=>b.DueDate==through).Sum(b=>b.PayableCents),
            group.Where(b=>b.DueDate>through).Sum(b=>b.PayableCents),group.Where(b=>b.DueDate is null).Sum(b=>b.PayableCents),group.Sum(b=>b.CreditCents))).ToList();
    }
}
