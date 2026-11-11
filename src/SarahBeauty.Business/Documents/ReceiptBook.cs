using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed class ReceiptBook(BusinessStore store)
{
    private readonly RecordStore<ReceiptDraft> _drafts=new(store,"ReceiptDraft");
    internal Action<string>? FailurePoint {get;set;}
    public List<ReceiptDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);

    public ReceiptDraft SaveDraft(OwnerSession owner,ReceiptDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id};
        return store.Write(owner,"Save receipt draft",draft.Id,db=>
        {
            if(draft.Status!="Draft" || draft.IssuedVersionId.Length>0 || (draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft"))
                throw new ArgumentException("Issued receipts cannot be edited. Reprint the saved version.");
            if(CorrectionRules.Original(db,owner,draft.PaymentId).Kind is not (FinancialKind.Receipt or FinancialKind.AssetDisposal))
                throw new ArgumentException("Choose an approved customer payment or asset-sale receipt.");
            return _drafts.Save(db,owner,draft with{ReviewedWatermark=checked(ReceiptSnapshot.Watermark(db,owner)+1)});
        });
    }

    public ClientSnapshot Preview(OwnerSession owner,ReceiptDraft draft)=>store.Read(db=>
    {store.RequireSession(db,owner);return ReceiptSnapshot.Build(db,store,owner,draft,"DRAFT");});

    public DocumentVersion Issue(OwnerSession owner,string draftId,int revision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var prior=DocumentVersions.Find(db,owner,draftId:draftId);
            if(prior is not null)return prior;
            var draft=_drafts.Get(db,owner,draftId);
            if(draft.Revision!=revision || draft.Status!="Draft")throw new ArgumentException("Reload the changed receipt draft.");
            return Issue(db,owner,draft);
        });
    }

    private DocumentVersion Issue(DbWork db,OwnerSession owner,ReceiptDraft draft)
    {
        if(db.Scalar("SELECT VersionId FROM ReceiptHeaders WHERE PaymentId=$id;",("$id",draft.PaymentId)) is not null)
            throw new ArgumentException("This payment already has an issued receipt. Open and reprint it.");
        var source=CorrectionRules.Original(db,owner,draft.PaymentId);
        string number=DocumentVersions.AllocateNumber(db,owner,DocumentKind.Receipt);
        var snapshot=ReceiptSnapshot.Build(db,store,owner,draft,number);
        string document=Guid.NewGuid().ToString();
        db.Execute("""
            INSERT INTO Documents (Id,BusinessId,Type,Number,BookingId,CreatedBy,CreatedAtUtc)
            VALUES ($id,$business,'Receipt',$number,$booking,$owner,$at);
            """,("$id",document),("$business",owner.BusinessId),("$number",number),("$booking",source.BookingId.Length==0?null:source.BookingId),
            ("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        var issued=DocumentVersions.Save(db,owner,new DocumentDraft{Id=draft.Id,DocumentId=document,BookingId=source.BookingId,Kind=DocumentKind.Receipt},snapshot);
        db.Execute("INSERT INTO ReceiptHeaders (VersionId,PaymentId) VALUES ($version,$payment);",("$version",issued.Id),("$payment",draft.PaymentId));
        FailurePoint?.Invoke("receipt-document");
        _drafts.Save(db,owner,draft with{Status="Issued",IssuedVersionId=issued.Id});
        BusinessStore.Audit(db,owner,"Issue payment receipt",issued.Id);
        return issued;
    }
}
