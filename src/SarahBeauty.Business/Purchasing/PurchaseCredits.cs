using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class PurchaseCredits
{
    internal static List<PurchaseCredit> Approved(DbWork db,BusinessStore store,OwnerSession owner,string bill,DateOnly through)=>
        new RecordStore<PurchaseCredit>(store,"PurchaseCredit").List(db,owner).Where(c=>c.Status=="Approved" && c.PurchaseId==bill && c.Date<=through).ToList();

    internal static List<PurchaseCreditRemaining> Remaining(DbWork db,BusinessStore store,OwnerSession owner,Purchase bill)
    {
        if(bill.Status!="Approved")throw new ArgumentException("Choose an approved supplier bill.");
        var credits=Lines(db,store,owner,bill.Id,DateOnly.MaxValue);
        return bill.Lines.Select(l=>new PurchaseCreditRemaining(l.Id,l.Description,l.GrossCents!.Value-credits.Where(c=>c.LineId==l.Id).Sum(c=>c.Cents),
            l.TaxPaidCents!.Value-credits.Where(c=>c.LineId==l.Id).Sum(c=>c.TaxCents!.Value),l.BusinessCents!.Value-credits.Where(c=>c.LineId==l.Id).Sum(c=>c.BusinessCents))).ToList();
    }

    internal static List<PurchaseCreditLine> Lines(DbWork db,BusinessStore store,OwnerSession owner,string bill,DateOnly date)=>
        Approved(db,store,owner,bill,date).SelectMany(c=>c.Lines.Select(l=>l with{Cents=c.Sign*l.Cents,
            TaxCents=c.Sign*l.TaxCents,BusinessCents=c.Sign*l.BusinessCents})).ToList();
}
