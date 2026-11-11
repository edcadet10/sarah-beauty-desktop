using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class PaymentApplications(BusinessStore store)
{
    private readonly RecordStore<PaymentApplicationDraft> _drafts=new(store,"PaymentApplicationDraft");
    internal Action<string>? FailurePoint {get;set;}
    public List<PaymentApplicationDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);
    public List<PaymentApplication> History(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return ApplicationWriter.List(db,owner);});

    public ValueBalance Available(OwnerSession owner,string paymentId,DateOnly date)=>store.Read(db=>
    {store.RequireSession(db,owner);return ValueAvailability.Payment(db,owner,paymentId,date);});

    public List<InvoiceDue> Outstanding(OwnerSession owner,DateOnly date)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return DocumentVersions.List(db,owner).Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void")
            .SelectMany(v=>ValueAvailability.Invoice(db,owner,v.Id,date)).ToList();
    });

    public PaymentApplicationDraft SaveDraft(OwnerSession owner,PaymentApplicationDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id};
        return store.Write(owner,"Save allocation draft",draft.Id,db=>
        {
            if(draft.Status!="Draft" || draft.GroupId.Length>0 || (draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft"))
                throw new ArgumentException("Approved applications need a dated reversal.");
            if(draft.ReversesId.Length>0)
            {
                var original=ApplicationWriter.Original(db,owner,draft.ReversesId);
                draft=draft with{PaymentId=original.PaymentId??"",CreditId=original.CreditId??"",InvoiceVersionId=original.InvoiceVersionId,MilestoneId=original.MilestoneId};
            }
            var key=ApplicationSource.Key(draft);
            return _drafts.Save(db,owner,draft with{SourceGeneration=ValueState.Generation(db,owner,key.Kind,key.Id),
                InvoiceGeneration=ValueState.Generation(db,owner,"Invoice",draft.InvoiceVersionId)});
        });
    }

    public List<PaymentApplication> Preview(OwnerSession owner,PaymentApplicationDraft draft)=>store.Read(db=>
    {store.RequireSession(db,owner);return ApplicationPlan.Build(db,owner,draft);});

    public List<PaymentApplication> Approve(OwnerSession owner,string draftId,int expectedRevision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            if(db.Scalar("SELECT Id FROM ApplicationGroups WHERE BusinessId=$business AND DraftId=$id;",("$business",owner.BusinessId),("$id",draftId)) is string group)
                return ApplicationWriter.List(db,owner,group);
            var draft=_drafts.Get(db,owner,draftId);
            if(draft.Status!="Draft" || draft.Revision!=expectedRevision)throw new ArgumentException("Reload the changed application draft.");
            return Approve(db,owner,draft);
        });
    }

    internal List<PaymentApplication> Approve(DbWork db,OwnerSession owner,PaymentApplicationDraft draft)
    {
        var rows=ApplicationWriter.Save(db,owner,draft,ApplicationPlan.Build(db,owner,draft),FailurePoint);
        if(draft.CreditId.Length>0)CreditValues.ValidateHistory(db,owner,draft.CreditId);
        else ValueAvailability.ValidatePaymentHistory(db,owner,draft.PaymentId);
        ValueAvailability.ValidateInvoiceHistory(db,owner,draft.InvoiceVersionId);
        var key=ApplicationSource.Key(draft);
        ValueState.Advance(db,owner,key.Kind,key.Id);
        ValueState.Advance(db,owner,"Invoice",draft.InvoiceVersionId);
        _drafts.Save(db,owner,draft with{Status="Posted",GroupId=rows[0].GroupId});
        BusinessStore.Audit(db,owner,draft.ReversesId.Length==0?"Apply receipt":"Reverse application",draft.Id);
        FailurePoint?.Invoke("application-audit");
        return rows;
    }
}
