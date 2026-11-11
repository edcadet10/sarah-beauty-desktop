using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal sealed record PurchaseLineBalance(string LineId,long Gross,long Business);

internal static class PurchaseBalances
{
    internal static List<PurchaseLineBalance> Lines(DbWork db,BusinessStore store,OwnerSession owner,Purchase bill,DateOnly date)
    {
        var credits=PurchaseCredits.Lines(db,store,owner,bill.Id,date);
        var paid=PurchasePayments.Rows(db,bill.Id).Where(p=>p.Date<=date).ToList();
        return bill.Lines.Select(l=>new PurchaseLineBalance(l.Id,checked(l.GrossCents!.Value-credits.Where(c=>c.LineId==l.Id).Sum(c=>c.Cents)-paid.Where(p=>p.LineId==l.Id).Sum(p=>p.Sign*p.Cents)),
            checked(l.BusinessCents!.Value-credits.Where(c=>c.LineId==l.Id).Sum(c=>c.BusinessCents)-paid.Where(p=>p.LineId==l.Id).Sum(p=>p.Sign*p.BusinessCents)))).ToList();
    }

    internal static PurchaseDue Due(DbWork db,BusinessStore store,OwnerSession owner,Purchase bill,DateOnly date)
    {
        var lines=Lines(db,store,owner,bill,date);
        return new(bill.Id,bill.SupplierId,bill.Name,bill.PurchaseDate!.Value,bill.DueDate,lines.Sum(l=>l.Gross))
            {PayableCents=lines.Sum(l=>Math.Max(0,l.Gross)),CreditCents=lines.Sum(l=>Math.Max(0,-l.Gross))};
    }
}
