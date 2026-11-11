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
        if(source.Kind is not (FinancialKind.PurchasePayment or FinancialKind.SupplierCreditUse) || draft.Date<source.Date
            || db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id;",("$id",draft.OriginalId)) is not null)
            throw new ArgumentException("Choose an unreversed approved supplier payment and a refund date on or after it.");
        if(draft.PurchaseId.Length>0 && draft.PurchaseId!=source.PurchaseId)throw new ArgumentException("Keep the original supplier bill for this refund.");
        if(!new[]{"Business cash","Reverse owner contribution","Reduce owner payable","Owner withdrawal"}.Contains(draft.SupplierRefundDestination))
            throw new ArgumentException("Choose where the refund actually goes and its owner treatment.");
        if(draft.SupplierRefundDestination=="Reverse owner contribution" && source.PurchaseFunding!="Owner contribution"
            || draft.SupplierRefundDestination=="Reduce owner payable" && source.PurchaseFunding!="Owner reimbursable")
            throw new ArgumentException("This refund treatment does not match the original owner funding.");
        bool earlierDraw=PurchasePayments.Rows(db,source.PurchaseId).Any(r=>r.MovementId==draft.OriginalId && r.Sign==1 && r.DrawCents>0);
        bool keepOwner=draft.SupplierRefundDestination!="Owner withdrawal" || earlierDraw;
        if(keepOwner && draft.EconomicOwnerId.Length>0 && draft.EconomicOwnerId!=source.EconomicOwnerId)
            throw new ArgumentException("Keep the original owner for the restored personal or funding share.");
        return draft with{PurchaseId=source.PurchaseId,SupplierId=source.SupplierId,PurchaseFunding=source.PurchaseFunding,
            EconomicOwnerId=keepOwner?source.EconomicOwnerId:draft.EconomicOwnerId};
    }

    internal static List<PurchaseSettlement> Rows(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        if(draft.PurchaseGeneration!=ValueState.Generation(db,owner,"Purchase",draft.PurchaseId))
            throw new ArgumentException("The supplier balance changed. Save and review this refund again.");
        var remaining=Available(db,draft);
        if(remaining.Count==0 || draft.AmountCents>remaining.Sum(r=>r.Share.Gross))
            throw new ArgumentException("The refund exceeds the original supplier payment's remaining gross or business share.");
        long[] shares=Allocate(draft,remaining);
        return remaining.Select((r,i)=>
        {
            var share=r.Share.Portion(shares[i]);
            return new PurchaseSettlement(Guid.NewGuid().ToString(),"",draft.PurchaseId,r.Row.LineId,draft.Date!.Value,share.Gross,share.Business,null,-1,r.Row.Id,share.Draw);
        }).Where(r=>r.Cents>0).ToList();
    }

    internal static List<RefundAvailable> Available(DbWork db,FinancialDraft draft)
    {
        var all=PurchasePayments.Rows(db,draft.PurchaseId);
        return all.Where(r=>r.MovementId==draft.OriginalId && r.Sign==1 && r.ReversesId is null).Select(r=>
        {
            var used=all.Where(f=>(f.RefundsId==r.Id || f.ConsumesId==r.Id) && f.Date<=draft.Date).ToList();
            var share=new SupplierFundingShare(r.Cents+used.Sum(f=>f.Sign*f.Cents),r.BusinessCents+used.Sum(f=>f.Sign*f.BusinessCents),r.DrawCents+used.Sum(f=>f.Sign*f.DrawCents));
            _=share.Portion(0);
            return new RefundAvailable(r,share);
        }).ToList();
    }

    private static long[] Allocate(FinancialDraft draft,List<RefundAvailable> available)
    {
        var selected=draft.SupplierRefundPortions;
        if(selected.Length==0)return PaymentPlan.Allocate(draft.AmountCents,available.Select(a=>a.Share.Gross).ToArray());
        if(selected.Select(p=>p.LineId).Distinct().Count()!=selected.Length || selected.Any(p=>p.GrossCents<=0)
            || selected.Sum(p=>p.GrossCents)!=draft.AmountCents || selected.Any(p=>!available.Any(a=>a.Row.LineId==p.LineId && p.GrossCents<=a.Share.Gross)))
            throw new ArgumentException("Choose distinct original payment lines whose available refund amounts add up to the total refund.");
        return available.Select(a=>selected.SingleOrDefault(p=>p.LineId==a.Row.LineId)?.GrossCents??0).ToArray();
    }

    internal sealed record RefundAvailable(PurchaseSettlement Row,SupplierFundingShare Share);

    internal static List<JournalLine> Lines(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        var purchase=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId);
        var rows=Rows(db,owner,draft);
        long gross=rows.Sum(r=>r.Cents),business=rows.Sum(r=>r.BusinessCents),draw=rows.Sum(r=>r.DrawCents),personal=checked(gross-business-draw);
        if(draft.SupplierRefundDestination!="Business cash" && business>0 && db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
            ("$id",draft.EconomicOwnerId),("$business",owner.BusinessId)) is null)throw new ArgumentException("Identify the owner involved in this refund.");
        var lines=PurchaseCostReturns.Lines(db,owner,purchase,draft,accounts,rows);
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        if(draft.SupplierRefundDestination=="Business cash")
        {
            lines.Add(new(draft.CashAccountId,gross,0));
            if(draw>0)lines.Add(new(Code("3100"),0,draw));
            if(personal>0)lines.Add(new(Code(draft.PurchaseFunding switch{"Owner contribution"=>"3000","Owner reimbursable"=>"2100",_=>"3100"}),0,personal));
        }
        else if(draft.SupplierRefundDestination=="Owner withdrawal")
        {
            if(business>0)lines.Add(new(Code("3100"),business,0));
        }
        else
        {
            if(business+draw>0)lines.Add(new(Code(draft.SupplierRefundDestination=="Reverse owner contribution"?"3000":"2100"),checked(business+draw),0));
            if(draw>0)lines.Add(new(Code("3100"),0,draw));
        }
        return lines;
    }

    internal static void ValidateHistory(DbWork db,string bill)
    {
        var rows=PurchasePayments.Rows(db,bill);
        foreach(var original in rows.Where(r=>r.Sign==1 && r.ReversesId is null && r.RefundsId is null))
        {
            var refunds=rows.Where(r=>r.RefundsId==original.Id || r.ConsumesId==original.Id).ToList();
            foreach(var date in refunds.Select(r=>r.Date).Distinct())
            {
                long gross=original.Cents+refunds.Where(r=>r.Date<=date).Sum(r=>r.Sign*r.Cents);
                long business=original.BusinessCents+refunds.Where(r=>r.Date<=date).Sum(r=>r.Sign*r.BusinessCents);
                long draw=original.DrawCents+refunds.Where(r=>r.Date<=date).Sum(r=>r.Sign*r.DrawCents);
                if(gross<0 || gross>original.Cents || business<0 || business>original.BusinessCents || business>gross
                    || draw<0 || draw>original.DrawCents || draw>gross-business || gross-business-draw>original.Cents-original.BusinessCents-original.DrawCents)
                    throw new ArgumentException("A dated refund or reversal would exceed its original supplier payment share.");
            }
        }
    }
}
