using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class PurchaseValidation
{
    internal static void Validate(DbWork db,BusinessStore store,OwnerSession owner,Purchase value)
    {
        if(string.IsNullOrWhiteSpace(value.Name) || value.PurchaseDate is null || value.DueDate<value.PurchaseDate || value.Lines.Length==0)
            throw new ArgumentException("Enter a bill description, actual purchase date, valid due date, and at least one line.");
        var supplier=new RecordStore<Supplier>(store,"Supplier").Get(db,owner,value.SupplierId);
        if(supplier.Archived)throw new ArgumentException("Choose an active supplier for this bill.");
        if(value.Lines.Select(l=>l.Id).Distinct().Count()!=value.Lines.Length || value.Lines.Any(l=>!Guid.TryParse(l.Id,out _)))
            throw new ArgumentException("Each purchase line needs its permanent identity.");
        var accounts=ChartOfAccounts.List(db,owner);
        foreach(var line in value.Lines)Line(db,store,owner,line,accounts);
        Rules.Money.Positive(value.Lines.Sum(l=>l.GrossCents!.Value));
        if(value.ReliableReference && string.IsNullOrWhiteSpace(value.SupplierReference))
            throw new ArgumentException("Enter the supplier's reliable invoice reference.");
        var others=new RecordStore<Purchase>(store,"Purchase").List(db,owner);
        if(value.ReliableReference && others.Any(p=>p.Id!=value.Id && p.Status=="Approved" && p.SupplierId==value.SupplierId
            && p.SupplierReference.Trim().Equals(value.SupplierReference.Trim(),StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("This supplier invoice reference was already approved. Review the original bill.");
    }

    private static void Line(DbWork db,BusinessStore store,OwnerSession owner,PurchaseLine line,List<Account> accounts)
    {
        if(string.IsNullOrWhiteSpace(line.Description) || line.Quantity<=0 || line.Quantity>100000 || line.UnitCostCents<0
            || line.TaxPaidCents is null or <0 || line.BusinessUseFraction is null or <0 or >1)
            throw new ArgumentException("Resolve each line's description, quantity, price, actual tax paid, and business-use share.");
        var account=accounts.SingleOrDefault(a=>a.Id==line.AccountId && a.Active && a.CashKind=="None" && a.Type is "Expense" or "Asset")
            ??throw new ArgumentException("Choose an active expense or noncash asset account for each business share.");
        if(account.Code=="1590")throw new ArgumentException("A purchase cannot be classified as accumulated depreciation.");
        if(account.Code=="1500" && line.AssetId.Length==0)throw new ArgumentException("Link the individual equipment asset.");
        if(line.AssetId.Length>0)
        {
            var asset=new RecordStore<Asset>(store,"Asset").Get(db,owner,line.AssetId);
            if(account.Type!="Asset" || asset.Archived || asset.DisposedDate.HasValue || asset.BusinessUseFraction!=line.BusinessUseFraction)
                throw new ArgumentException("Use an active asset with the same confirmed business-use share and an asset classification.");
        }
        if(line.BookingId.Length>0)_=new RecordStore<Booking>(store,"Booking").Get(db,owner,line.BookingId);
    }
}
