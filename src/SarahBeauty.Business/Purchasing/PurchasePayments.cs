using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

public sealed class PurchasePayments(BusinessStore store)
{
    public List<SupplierRefundItem> RefundableLines(OwnerSession owner,string movement,DateOnly date)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        var draft=PurchaseRefundPlan.Prepare(db,owner,new FinancialDraft{OriginalId=movement,Date=date});
        var purchase=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId);
        return PurchaseRefundPlan.Available(db,draft).Select(a=>
        {
            var line=purchase.Lines.Single(l=>l.Id==a.Row.LineId);
            return new SupplierRefundItem(line.Id,line.Description,a.Share.Gross,line.AssetId);
        }).ToList();
    });

    public List<PurchaseDue> Outstanding(OwnerSession owner,DateOnly date)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        return Outstanding(db,store,owner,date);
    });

    internal static List<PurchaseDue> Outstanding(DbWork db,BusinessStore store,OwnerSession owner,DateOnly date)=>
        new RecordStore<Purchase>(store,"Purchase").List(db,owner).Where(p=>p.Status=="Approved" && p.PurchaseDate<=date)
            .Select(p=>PurchaseBalances.Due(db,store,owner,p,date)).ToList();

    internal static FinancialDraft Prepare(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        if(draft.Kind==FinancialKind.SupplierCreditUse)return SupplierCreditSource.Prepare(db,store,owner,draft);
        if(draft.Kind==FinancialKind.PurchaseRefund)return PurchaseRefundPlan.Prepare(db,owner,draft);
        if(draft.Kind!=FinancialKind.PurchasePayment)return draft;
        var purchase=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId);
        if(draft.SupplierId.Length>0 && draft.SupplierId!=purchase.SupplierId)throw new ArgumentException("The payment must retain its supplier bill's supplier.");
        return draft with{SupplierId=purchase.SupplierId};
    }

    internal static List<JournalLine> Lines(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,List<Account> accounts)=>
        PurchasePaymentPlan.Lines(db,store,owner,new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId),draft,accounts);

    internal static void AfterPosting(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,string movement)
    {
        List<PurchaseSettlement> rows;
        if(draft.Kind==FinancialKind.PurchasePayment)
        {
            var purchase=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId);
            rows=PurchasePaymentPlan.Rows(db,store,owner,purchase,draft);
        }
        else if(draft.Kind==FinancialKind.PurchaseRefund)rows=PurchaseRefundPlan.Rows(db,owner,draft);
        else if(draft.Kind==FinancialKind.SupplierCreditUse)
        {
            var plan=SupplierCreditUsePlan.Build(db,store,owner,draft);
            rows=[..plan.Out,..plan.In];
        }
        else if(draft.Kind==FinancialKind.Reversal && draft.PurchaseId.Length>0)
            rows=MovementRows(db,draft.OriginalId)
                .Select(r=>r with{Id=Guid.NewGuid().ToString(),Date=draft.Date!.Value,ReversesId=r.Id,Sign=-r.Sign}).ToList();
        else return;
        foreach(var row in rows)Save(db,row,movement);
        foreach(string id in rows.Select(r=>r.PurchaseId).Distinct())
        {
            var bill=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,id);
            foreach(var date in Rows(db,id).Select(r=>r.Date).Distinct())ValidateHistory(db,bill,date);
            PurchaseRefundPlan.ValidateHistory(db,id);
            PurchaseCredits.ValidateHistory(db,store,owner,id);
            ValueState.Advance(db,owner,"Purchase",id);
        }
    }

    internal static bool AllowsEmpty(DbWork db,OwnerSession owner,FinancialDraft draft)=>
        draft.Kind==FinancialKind.SupplierCreditUse || draft.Kind==FinancialKind.PurchasePayment && draft.PurchaseFunding!="Business cash"
        || draft.Kind==FinancialKind.PurchaseRefund && draft.SupplierRefundDestination!="Business cash"
        || draft.Kind==FinancialKind.Reversal && draft.PurchaseId.Length>0 && CorrectionRules.Original(db,owner,draft.OriginalId).Kind is FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse;

    private static void ValidateHistory(DbWork db,Purchase bill,DateOnly date)
    {
        var rows=Rows(db,bill.Id).Where(r=>r.Date<=date).ToList();
        foreach(var line in bill.Lines)
        {
            long gross=line.GrossCents!.Value-rows.Where(r=>r.LineId==line.Id).Sum(r=>r.Sign*r.Cents);
            long business=line.BusinessCents!.Value-rows.Where(r=>r.LineId==line.Id).Sum(r=>r.Sign*r.BusinessCents);
            if(gross<0 || business<0 || business>gross)throw new ArgumentException("The payment would overpay a supplier bill line at an affected date.");
        }
    }

    internal static List<PurchaseSettlement> Rows(DbWork db,string bill)=>ReadRows(db,"PurchaseId",bill);
    private static List<PurchaseSettlement> MovementRows(DbWork db,string movement)=>ReadRows(db,"MovementId",movement);

    private static List<PurchaseSettlement> ReadRows(DbWork db,string column,string id)
    {
        if(column is not ("PurchaseId" or "MovementId"))throw new ArgumentException("Unsupported supplier settlement dimension.");
        return db.Read($"""
            SELECT Id,MovementId,PurchaseId,LineId,Date,Cents,BusinessCents,ReversesId,Sign,RefundsId,DrawCents,ConsumesId FROM PurchaseSettlements WHERE {column}=$id;
            """,r=>new PurchaseSettlement(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),DateOnly.Parse(r.GetString(4)),r.GetInt64(5),r.GetInt64(6),r.IsDBNull(7)?null:r.GetString(7),r.GetInt32(8),r.IsDBNull(9)?null:r.GetString(9),r.GetInt64(10),r.IsDBNull(11)?null:r.GetString(11)),("$id",id));
    }

    private static void Save(DbWork db,PurchaseSettlement row,string movement)=>db.Execute("""
        INSERT INTO PurchaseSettlements (Id,MovementId,PurchaseId,LineId,Date,Cents,BusinessCents,ReversesId,Sign,RefundsId,DrawCents,ConsumesId)
        VALUES ($id,$movement,$purchase,$line,$date,$gross,$business,$reverse,$sign,$refund,$draw,$consumes);
        """,("$id",row.Id),("$movement",movement),("$purchase",row.PurchaseId),("$line",row.LineId),("$date",row.Date.ToString("yyyy-MM-dd")),
        ("$gross",row.Cents),("$business",row.BusinessCents),("$reverse",row.ReversesId),("$sign",row.Sign),("$refund",row.RefundsId),
        ("$draw",row.DrawCents),("$consumes",row.ConsumesId));
}
