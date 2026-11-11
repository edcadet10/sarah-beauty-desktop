using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

public sealed class PurchaseCreditBook(BusinessStore store)
{
    private readonly RecordStore<PurchaseCredit> _records=new(store,"PurchaseCredit");
    internal Action<string>? FailurePoint {get;set;}
    public List<PurchaseCredit> List(OwnerSession owner)=>_records.List(owner);

    public PurchaseCredit ReversalDraft(OwnerSession owner,string id)
    {
        var original=List(owner).SingleOrDefault(c=>c.Id==id && c.Status=="Approved" && c.Sign==1)
            ??throw new ArgumentException("Choose an original approved supplier credit.");
        return new PurchaseCredit{PurchaseId=original.PurchaseId,ReversesId=id,Sign=-1,Lines=original.Lines,Date=DateOnly.FromDateTime(DateTime.Today)};
    }

    public List<PurchaseCreditRemaining> Remaining(OwnerSession owner,string bill)=>store.Read(db=>
    {store.RequireSession(db,owner);return PurchaseCredits.Remaining(db,store,owner,new RecordStore<Purchase>(store,"Purchase").Get(db,owner,bill));});

    public PurchaseCredit SaveDraft(OwnerSession owner,PurchaseCredit value)
    {
        value=value with{Id=value.Id.Length==0?Guid.NewGuid().ToString():value.Id,Name=value.Name.Trim()};
        return store.Write(owner,"Save supplier credit draft",value.Id,db=>
        {
            if(value.Status!="Draft" || value.ApprovedBy.Length>0 || value.ApprovedAtUtc.Length>0
                || (value.Revision>0 && _records.Get(db,owner,value.Id).Status!="Draft"))throw new ArgumentException("Approved supplier credits cannot be overwritten.");
            if(value.ReversesId.Length>0)
            {
                var original=_records.Get(db,owner,value.ReversesId);
                value=value with{PurchaseId=original.PurchaseId,Sign=-1,Lines=original.Lines};
            }
            else if(value.Sign!=1)throw new ArgumentException("Use a linked reversal to restore an approved supplier reduction.");
            _=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,value.PurchaseId);
            return _records.Save(db,owner,value with{PurchaseGeneration=ValueState.Generation(db,owner,"Purchase",value.PurchaseId)});
        });
    }

    public PurchaseCredit Preview(OwnerSession owner,PurchaseCredit value)=>store.Read(db=>
    {store.RequireSession(db,owner);return Prepare(db,owner,value);});

    public PurchaseCredit Approve(OwnerSession owner,string id,int revision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var value=_records.Get(db,owner,id);
            if(value.Status=="Approved")return value;
            if(value.Status!="Draft" || value.Revision!=revision)throw new ArgumentException("Reload the changed supplier credit.");
            value=Prepare(db,owner,value);
            var result=_records.Save(db,owner,value with{Status="Approved",ApprovedBy=owner.OwnerId,ApprovedAtUtc=DateTimeOffset.UtcNow.ToString("O")});
            ValidateHistory(db,owner,value.PurchaseId);
            ValueState.Advance(db,owner,"Purchase",value.PurchaseId);
            FailurePoint?.Invoke("supplier-credit");
            BusinessStore.Audit(db,owner,"Approve supplier bill credit",id);
            return result;
        });
    }

    private PurchaseCredit Prepare(DbWork db,OwnerSession owner,PurchaseCredit value)
    {
        var bill=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,value.PurchaseId);
        if(bill.Status!="Approved" || value.Date is null || value.Date<bill.PurchaseDate || string.IsNullOrWhiteSpace(value.Name)
            || value.Lines.Length==0 || value.Lines.Select(l=>l.LineId).Distinct().Count()!=value.Lines.Length)
            throw new ArgumentException("Choose an approved supplier bill, a valid credit date/reason, and distinct original lines.");
        PostingValidation.CheckClosedPeriod(db,owner,value.Date.Value);
        if(value.PurchaseGeneration!=ValueState.Generation(db,owner,"Purchase",bill.Id))throw new ArgumentException("The supplier bill changed. Save and review the credit again.");
        if(value.ReliableReference && string.IsNullOrWhiteSpace(value.SupplierReference))throw new ArgumentException("Enter the supplier credit's reliable reference.");
        var others=ListForSupplier(db,owner,bill.SupplierId);
        if(value.ReliableReference && others.Any(c=>c.Id!=value.Id && c.SupplierReference.Trim().Equals(value.SupplierReference.Trim(),StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("This supplier credit reference was already approved.");
        var remaining=PurchaseCredits.Remaining(db,store,owner,bill);
        if(value.ReversesId.Length>0)return PrepareReversal(db,owner,value);
        if(value.Sign!=1)throw new ArgumentException("Choose a valid supplier credit action.");
        return value with{Lines=value.Lines.Select(l=>PrepareLine(l,remaining.SingleOrDefault(r=>r.LineId==l.LineId)
            ??throw new ArgumentException("A credit line does not belong to the original bill."))).ToArray()};
    }

    private PurchaseCredit PrepareReversal(DbWork db,OwnerSession owner,PurchaseCredit value)
    {
        var original=_records.Get(db,owner,value.ReversesId);
        if(original.Status!="Approved" || original.Sign!=1 || original.PurchaseId!=value.PurchaseId || value.Date<original.Date
            || _records.List(db,owner).Any(c=>c.Status=="Approved" && c.ReversesId==original.Id))
            throw new ArgumentException("Choose an unreversed original supplier credit and a date on or after it.");
        return value with{Sign=-1,Lines=original.Lines};
    }

    private static PurchaseCreditLine PrepareLine(PurchaseCreditLine line,PurchaseCreditRemaining remaining)
    {
        if(line.Cents<=0 || line.Cents>remaining.Cents || line.TaxCents is null or <0 || line.TaxCents>line.Cents
            || line.TaxCents>remaining.TaxCents || remaining.TaxCents-line.TaxCents>remaining.Cents-line.Cents)
            throw new ArgumentException("Resolve the supplier credit's gross and included tax within the original remaining line.");
        long business=line.Cents==remaining.Cents?remaining.BusinessCents:Money.Proportion(remaining.BusinessCents,line.Cents,remaining.Cents);
        return line with{BusinessCents=business};
    }

    private List<PurchaseCredit> ListForSupplier(DbWork db,OwnerSession owner,string supplier)
    {
        var bills=new RecordStore<Purchase>(store,"Purchase").List(db,owner).Where(p=>p.SupplierId==supplier).Select(p=>p.Id).ToHashSet();
        return _records.List(db,owner).Where(c=>c.Status=="Approved" && bills.Contains(c.PurchaseId)).ToList();
    }

    private void ValidateHistory(DbWork db,OwnerSession owner,string id)
    {
        var bill=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,id);
        var dates=PurchaseCredits.Approved(db,store,owner,id,DateOnly.MaxValue).Select(c=>c.Date!.Value).Distinct();
        foreach(var date in dates)
        {
            var credits=PurchaseCredits.Lines(db,store,owner,id,date);
            foreach(var line in bill.Lines)
            {
                long gross=line.GrossCents!.Value-credits.Where(c=>c.LineId==line.Id).Sum(c=>c.Cents);
                long tax=line.TaxPaidCents!.Value-credits.Where(c=>c.LineId==line.Id).Sum(c=>c.TaxCents!.Value);
                long business=line.BusinessCents!.Value-credits.Where(c=>c.LineId==line.Id).Sum(c=>c.BusinessCents);
                if(gross<0 || gross>line.GrossCents || tax<0 || tax>line.TaxPaidCents || tax>gross || business<0 || business>line.BusinessCents || business>gross)
                    throw new ArgumentException("The supplier reduction or reversal exceeds an original line at an affected historical date.");
            }
        }
    }
}
