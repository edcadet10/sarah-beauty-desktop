using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class PurchasePaymentPlan
{
    internal static List<PurchaseSettlement> Rows(DbWork db,OwnerSession owner,Purchase purchase,FinancialDraft draft)
    {
        if(purchase.Status!="Approved" || draft.Date<purchase.PurchaseDate || draft.PurchaseGeneration!=ValueState.Generation(db,owner,"Purchase",purchase.Id))
            throw new ArgumentException("Choose an approved bill, a valid payment date, and review its current unpaid balance.");
        var previous=PurchasePayments.Rows(db,purchase.Id).Where(r=>r.Date<=draft.Date).ToList();
        var available=purchase.Lines.Select(l=>new{Line=l,Gross=l.GrossCents!.Value-previous.Where(p=>p.LineId==l.Id).Sum(p=>p.Sign*p.Cents),
            Business=l.BusinessCents!.Value-previous.Where(p=>p.LineId==l.Id).Sum(p=>p.Sign*p.BusinessCents)}).ToList();
        if(draft.AmountCents>available.Sum(a=>a.Gross) || available.Any(a=>a.Gross<0 || a.Business<0 || a.Business>a.Gross))
            throw new ArgumentException("The payment exceeds the supplier bill's unpaid value.");
        long[] shares=PaymentPlan.Allocate(draft.AmountCents,available.Select(a=>a.Gross).ToArray());
        return available.Select((a,i)=>new PurchaseSettlement(Guid.NewGuid().ToString(),"",purchase.Id,a.Line.Id,draft.Date!.Value,shares[i],
            shares[i]==0?0:shares[i]==a.Gross?a.Business:Money.Proportion(a.Business,shares[i],a.Gross),null)).Where(r=>r.Cents>0).ToList();
    }

    internal static List<JournalLine> Lines(DbWork db,BusinessStore store,OwnerSession owner,Purchase purchase,FinancialDraft draft,List<Account> accounts)
    {
        var rows=Rows(db,owner,purchase,draft);
        if(!new[]{"Business cash","Owner contribution","Owner reimbursable"}.Contains(draft.PurchaseFunding))throw new ArgumentException("Choose how this supplier payment was actually funded.");
        long gross=rows.Sum(r=>r.Cents),business=rows.Sum(r=>r.BusinessCents),personal=gross-business;
        if((draft.PurchaseFunding!="Business cash" || personal>0) && db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
            ("$id",draft.EconomicOwnerId),("$business",owner.BusinessId)) is null)throw new ArgumentException("Identify the paying owner or the owner receiving the personal benefit.");
        var lines=new List<JournalLine>();
        foreach(var row in rows.Where(r=>r.BusinessCents>0))
        {
            var source=purchase.Lines.Single(l=>l.Id==row.LineId);
            var account=accounts.SingleOrDefault(a=>a.Id==source.AccountId && a.Active && a.CashKind=="None" && a.Type is "Expense" or "Asset")
                ??throw new ArgumentException("A bill classification changed or is inactive. Review it before posting.");
            if(account.Code=="1590" || (account.Code=="1500" && source.AssetId.Length==0) || (source.AssetId.Length>0 && account.Type!="Asset"))
                throw new ArgumentException("The bill's asset classification needs review before payment.");
            if(source.AssetId.Length>0)ValidateAsset(db,store,owner,source,draft.Date!.Value);
            lines.Add(new(account.Id,row.BusinessCents,0,source.AssetId));
        }
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        if(draft.PurchaseFunding=="Business cash")
        {
            if(personal>0)lines.Add(new(Code("3100"),personal,0));
            lines.Add(new(draft.CashAccountId,0,gross));
        }
        else if(business>0)lines.Add(new(Code(draft.PurchaseFunding=="Owner contribution"?"3000":"2100"),0,business));
        return lines;
    }

    private static void ValidateAsset(DbWork db,BusinessStore store,OwnerSession owner,PurchaseLine source,DateOnly date)
    {
        var asset=new RecordStore<Asset>(store,"Asset").Get(db,owner,source.AssetId);
        if(asset.Archived || asset.DisposedDate.HasValue || AssetDisposals.IsDisposed(db,owner,asset.Id,date)
            || asset.BusinessUseFraction!=source.BusinessUseFraction)throw new ArgumentException("The bill's asset is no longer available with the reviewed business-use share.");
    }
}
