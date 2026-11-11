using System.Globalization;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed class CreditNoteBook(BusinessStore store)
{
    private readonly RecordStore<CreditNoteDraft> _drafts=new(store,"CreditNoteDraft");
    internal Action<string>? FailurePoint {get;set;}
    public List<CreditNoteDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);

    public List<ChargeRemaining> Remaining(OwnerSession owner,string invoiceId)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        var invoice=DocumentVersions.Find(db,owner,id:invoiceId)??throw new ArgumentException("Choose an invoice from this business.");
        return invoice.Snapshot.Lines.Select(l=>
        {
            var used=CreditNoteReductions.Lines(db,invoiceId,l.ChargeId);
            return new ChargeRemaining(l.ChargeId,l.Description,l.Quantity-used.Sum(u=>u.Quantity),
                l.NetCents-used.Sum(u=>u.NetCents),l.TaxCents-used.Sum(u=>u.TaxCents));
        }).ToList();
    });

    public CreditNoteDraft SaveDraft(OwnerSession owner,CreditNoteDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id};
        return store.Write(owner,"Save credit note draft",draft.Id,db=>
        {
            if(draft.Status!="Draft" || draft.IssuedVersionId.Length>0 || (draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft"))
                throw new ArgumentException("Issued credit notes cannot be edited.");
            _=DocumentVersions.Find(db,owner,id:draft.InvoiceVersionId)??throw new ArgumentException("Choose an invoice from this business.");
            return _drafts.Save(db,owner,draft with{InvoiceGeneration=ValueState.Generation(db,owner,"Invoice",draft.InvoiceVersionId)});
        });
    }

    public ClientSnapshot Preview(OwnerSession owner,CreditNoteDraft draft)=>store.Read(db=>
    {store.RequireSession(db,owner);return CreditNotePlan.Build(db,owner,draft,"DRAFT").Snapshot;});

    public DocumentVersion Issue(OwnerSession owner,string draftId,int revision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var prior=DocumentVersions.Find(db,owner,draftId:draftId);
            if(prior is not null)return prior;
            var draft=_drafts.Get(db,owner,draftId);
            if(draft.Revision!=revision || draft.Status!="Draft")throw new ArgumentException("Reload the changed credit note draft.");
            return Issue(db,owner,draft);
        });
    }

    private DocumentVersion Issue(DbWork db,OwnerSession owner,CreditNoteDraft draft)
    {
        string number=DocumentVersions.AllocateNumber(db,owner,DocumentKind.CreditNote);
        var (invoice,snapshot,reductions)=CreditNotePlan.Build(db,owner,draft,number);
        string document=Guid.NewGuid().ToString();
        db.Execute("""
            INSERT INTO Documents (Id,BusinessId,Type,Number,BookingId,CreatedBy,CreatedAtUtc)
            VALUES ($id,$business,'CreditNote',$number,$booking,$owner,$at);
            """,("$id",document),("$business",owner.BusinessId),("$number",number),("$booking",invoice.BookingId),
            ("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        var issued=DocumentVersions.Save(db,owner,new DocumentDraft{Id=draft.Id,DocumentId=document,BookingId=invoice.BookingId,Kind=DocumentKind.CreditNote},snapshot);
        SaveReductions(db,issued,invoice,reductions);
        FailurePoint?.Invoke("credit-note-reductions");
        ValueAvailability.ValidateInvoiceHistory(db,owner,invoice.Id);
        ValueState.Advance(db,owner,"Invoice",invoice.Id);
        db.Execute("UPDATE BillingState SET Generation=Generation+1 WHERE BookingId=$id;",("$id",invoice.BookingId));
        _drafts.Save(db,owner,draft with{Status="Issued",IssuedVersionId=issued.Id});
        BusinessStore.Audit(db,owner,"Issue invoice credit note",issued.Id);
        FailurePoint?.Invoke("credit-note-audit");
        return issued;
    }

    private static void SaveReductions(DbWork db,DocumentVersion note,DocumentVersion invoice,List<MilestoneReduction> reductions)
    {
        db.Execute("INSERT INTO CreditNoteHeaders (VersionId,InvoiceVersionId,Date) VALUES ($id,$invoice,$date);",
            ("$id",note.Id),("$invoice",invoice.Id),("$date",note.Snapshot.IssueDate.ToString("yyyy-MM-dd")));
        foreach(var line in note.Snapshot.Lines)
            db.Execute("""
                INSERT INTO CreditNoteLines (NoteVersionId,ChargeId,Quantity,NetCents,TaxCents,DiscountCents)
                VALUES ($note,$charge,$quantity,$net,$tax,$discount);
                """,("$note",note.Id),("$charge",line.ChargeId),("$quantity",line.Quantity.ToString(CultureInfo.InvariantCulture)),
                ("$net",line.NetCents),("$tax",line.TaxCents),("$discount",line.DiscountCents));
        foreach(var reduction in reductions)
            db.Execute("INSERT INTO CreditNoteMilestones (NoteVersionId,MilestoneId,Cents,TaxCents) VALUES ($note,$milestone,$cents,$tax);",
                ("$note",note.Id),("$milestone",reduction.MilestoneId),("$cents",reduction.Cents),("$tax",reduction.TaxCents));
    }
}
