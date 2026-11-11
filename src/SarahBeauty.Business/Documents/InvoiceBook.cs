using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed class InvoiceBook(BusinessStore store)
{
    private readonly RecordStore<InvoiceDraft> _drafts=new(store,"InvoiceDraft");
    public List<InvoiceDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);

    public List<ChargeRemaining> Remaining(OwnerSession owner,string quoteId)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        var quote=DocumentVersions.Find(db,owner,id:quoteId)??throw new ArgumentException("Quote not found.");
        return quote.Snapshot.Lines.Select(l=>
        {
            var used=InvoiceCoverage.Existing(db,quote.BookingId,l.ChargeId);
            return new ChargeRemaining(l.ChargeId,l.Description,l.Quantity-used.Sum(u=>u.Quantity),
                l.NetCents-used.Sum(u=>u.NetCents),l.TaxCents-used.Sum(u=>u.TaxCents));
        }).ToList();
    });

    public InvoiceDraft SaveDraft(OwnerSession owner,InvoiceDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id};
        return store.Write(owner,"Save invoice draft",draft.Id,db=>
        {
            if(draft.Status!="Draft" || draft.IssuedVersionId.Length>0 || (draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft"))
                throw new ArgumentException("Issued invoices require a credit or debit adjustment.");
            var quote=DocumentVersions.Find(db,owner,id:draft.QuoteVersionId)??throw new ArgumentException("Choose a quote.");
            return _drafts.Save(db,owner,draft with{CoverageGeneration=InvoiceCoverage.Generation(db,quote.BookingId)});
        });
    }

    public ClientSnapshot Preview(OwnerSession owner,InvoiceDraft draft)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return InvoiceSnapshot.Create(db,store,owner,draft,"DRAFT").Snapshot;
    });

    public DocumentVersion Issue(OwnerSession owner,string draftId,int expectedRevision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var existing=DocumentVersions.Find(db,owner,draftId:draftId);
            if(existing is not null)return existing;
            var draft=_drafts.Get(db,owner,draftId);
            if(draft.Revision!=expectedRevision || draft.Status!="Draft")throw new ArgumentException("Reload the changed invoice draft.");
            return Issue(db,owner,draft);
        });
    }

    private DocumentVersion Issue(DbWork db,OwnerSession owner,InvoiceDraft draft)
    {
        string number=DocumentVersions.AllocateNumber(db,owner,DocumentKind.Invoice);
        var (quote,snapshot)=InvoiceSnapshot.Create(db,store,owner,draft,number);
        string documentId=Guid.NewGuid().ToString();
        db.Execute("""
            INSERT INTO Documents (Id,BusinessId,Type,Number,BookingId,CreatedBy,CreatedAtUtc)
            VALUES ($id,$business,'Invoice',$number,$booking,$owner,$at);
            """,("$id",documentId),("$business",owner.BusinessId),("$number",number),("$booking",quote.BookingId),("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        var bridge=new DocumentDraft{Id=draft.Id,DocumentId=documentId,BookingId=quote.BookingId,Kind=DocumentKind.Invoice};
        var issued=DocumentVersions.Save(db,owner,bridge,snapshot);
        InvoiceCoverage.Save(db,quote,issued,draft.PayerCustomerId);
        _drafts.Save(db,owner,draft with{Status="Issued",IssuedVersionId=issued.Id});
        BusinessStore.Audit(db,owner,"Issue invoice",issued.Id);
        return issued;
    }
}
