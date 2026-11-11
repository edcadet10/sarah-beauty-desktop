using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class PurchaseRefundPlan
{
    internal static FinancialDraft Prepare(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        var source=CorrectionRules.Original(db,owner,draft.OriginalId);
        if(source.Kind!=FinancialKind.PurchasePayment || draft.Date<source.Date
            || db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id;",("$id",draft.OriginalId)) is not null)
            throw new ArgumentException("Choose an unreversed approved supplier payment and a refund date on or after it.");
        if(draft.PurchaseId.Length>0 && draft.PurchaseId!=source.PurchaseId)throw new ArgumentException("Keep the original supplier bill for this refund.");
        if(!new[]{"Business cash","Reverse owner contribution","Reduce owner payable","Owner withdrawal"}.Contains(draft.SupplierRefundDestination))
            throw new ArgumentException("Choose where the refund actually goes and its owner treatment.");
        if(draft.SupplierRefundDestination=="Reverse owner contribution" && source.PurchaseFunding!="Owner contribution"
            || draft.SupplierRefundDestination=="Reduce owner payable" && source.PurchaseFunding!="Owner reimbursable")
            throw new ArgumentException("This refund treatment does not match the original owner funding.");
        bool keepOwner=draft.SupplierRefundDestination!="Owner withdrawal";
        if(keepOwner && draft.EconomicOwnerId.Length>0 && draft.EconomicOwnerId!=source.EconomicOwnerId)
            throw new ArgumentException("Keep the original owner for the restored personal or funding share.");
        return draft with{PurchaseId=source.PurchaseId,SupplierId=source.SupplierId,PurchaseFunding=source.PurchaseFunding,
            EconomicOwnerId=keepOwner?source.EconomicOwnerId:draft.EconomicOwnerId};
    }

    internal static List<PurchaseSettlement> Rows(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        if(draft.PurchaseGeneration!=ValueState.Generation(db,owner,"Purchase",draft.PurchaseId))
            throw new ArgumentException("The supplier balance changed. Save and review this refund again.");
        var all=PurchasePayments.Rows(db,draft.PurchaseId);
        var remaining=all.Where(r=>r.MovementId==draft.OriginalId).Select(r=>new{Row=r,
            Gross=r.Cents+all.Where(f=>f.RefundsId==r.Id && f.Date<=draft.Date).Sum(f=>f.Sign*f.Cents),
            Business=r.BusinessCents+all.Where(f=>f.RefundsId==r.Id && f.Date<=draft.Date).Sum(f=>f.Sign*f.BusinessCents)}).ToList();
        if(remaining.Count==0 || remaining.Any(r=>r.Gross<0 || r.Business<0 || r.Business>r.Gross) || draft.AmountCents>remaining.Sum(r=>r.Gross))
            throw new ArgumentException("The refund exceeds the original supplier payment's remaining gross or business share.");
        long[] shares=PaymentPlan.Allocate(draft.AmountCents,remaining.Select(r=>r.Gross).ToArray());
        return remaining.Select((r,i)=>new PurchaseSettlement(Guid.NewGuid().ToString(),"",draft.PurchaseId,r.Row.LineId,draft.Date!.Value,shares[i],
            shares[i]==0?0:shares[i]==r.Gross?r.Business:Money.Proportion(r.Business,shares[i],r.Gross),null,-1,r.Row.Id)).Where(r=>r.Cents>0).ToList();
    }

    internal static List<JournalLine> Lines(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        var purchase=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId);
        var rows=Rows(db,owner,draft);
        long gross=rows.Sum(r=>r.Cents),business=rows.Sum(r=>r.BusinessCents),personal=gross-business;
        if(draft.SupplierRefundDestination!="Business cash" && business>0 && db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
            ("$id",draft.EconomicOwnerId),("$business",owner.BusinessId)) is null)throw new ArgumentException("Identify the owner involved in this refund.");
        var lines=rows.Where(r=>r.BusinessCents>0).Select(r=>
        {
            var original=purchase.Lines.Single(l=>l.Id==r.LineId);
            return new JournalLine(original.AccountId,0,r.BusinessCents,original.AssetId);
        }).ToList();
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        if(draft.SupplierRefundDestination=="Business cash")
        {
            lines.Add(new(draft.CashAccountId,gross,0));
            if(personal>0)lines.Add(new(Code(draft.PurchaseFunding switch{"Owner contribution"=>"3000","Owner reimbursable"=>"2100",_=>"3100"}),0,personal));
        }
        else if(business>0)lines.Add(new(Code(draft.SupplierRefundDestination switch{"Reverse owner contribution"=>"3000","Reduce owner payable"=>"2100",_=>"3100"}),business,0));
        return lines;
    }

    internal static void ValidateHistory(DbWork db,string bill)
    {
        var rows=PurchasePayments.Rows(db,bill);
        foreach(var original in rows.Where(r=>r.Sign==1 && r.ReversesId is null && r.RefundsId is null))
        {
            var refunds=rows.Where(r=>r.RefundsId==original.Id).ToList();
            foreach(var date in refunds.Select(r=>r.Date).Distinct())
            {
                long gross=original.Cents+refunds.Where(r=>r.Date<=date).Sum(r=>r.Sign*r.Cents);
                long business=original.BusinessCents+refunds.Where(r=>r.Date<=date).Sum(r=>r.Sign*r.BusinessCents);
                if(gross<0 || gross>original.Cents || business<0 || business>original.BusinessCents || business>gross)
                    throw new ArgumentException("A dated refund or reversal would exceed its original supplier payment share.");
            }
        }
    }
}
