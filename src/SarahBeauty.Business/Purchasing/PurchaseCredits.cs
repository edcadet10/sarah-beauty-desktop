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

    internal static void ValidateHistory(DbWork db,BusinessStore store,OwnerSession owner,string id)
    {
        var bill=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,id);
        var paid=PurchasePayments.Rows(db,id);
        var dates=Approved(db,store,owner,id,DateOnly.MaxValue).Select(c=>c.Date!.Value).Concat(paid.Select(p=>p.Date)).Distinct();
        foreach(var date in dates)
        {
            var credits=Lines(db,store,owner,id,date);
            foreach(var line in bill.Lines)ValidateLine(line,credits.Where(c=>c.LineId==line.Id),paid.Where(p=>p.LineId==line.Id && p.Date<=date));
        }
    }

    private static void ValidateLine(PurchaseLine line,IEnumerable<PurchaseCreditLine> credits,IEnumerable<PurchaseSettlement> paid)
    {
        long gross=line.GrossCents!.Value-credits.Sum(c=>c.Cents),tax=line.TaxPaidCents!.Value-credits.Sum(c=>c.TaxCents!.Value);
        long business=line.BusinessCents!.Value-credits.Sum(c=>c.BusinessCents);
        if(gross<0 || gross>line.GrossCents || tax<0 || tax>line.TaxPaidCents || tax>gross || business<0 || business>line.BusinessCents || business>gross)
            throw new ArgumentException("The supplier reduction or reversal exceeds an original line at an affected historical date.");
        long due=checked(gross-paid.Sum(p=>p.Sign*p.Cents)),dueBusiness=checked(business-paid.Sum(p=>p.Sign*p.BusinessCents));
        if(due>=0?dueBusiness<0 || dueBusiness>due:dueBusiness>0 || dueBusiness<due)
            throw new ArgumentException("This action conflicts with a payment's business-share cent allocation. Review the affected payment and adjustment dates.");
    }
}
