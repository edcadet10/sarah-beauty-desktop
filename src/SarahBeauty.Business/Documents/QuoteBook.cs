using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed class QuoteBook(BusinessStore store)
{
    private readonly RecordStore<DocumentDraft> _drafts=new(store,"DocumentDraft");
    public List<DocumentDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);
    public List<DocumentVersion> Versions(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return DocumentVersions.List(db,owner);});

    public ClientSnapshot Preview(OwnerSession owner,DocumentDraft draft)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return QuoteSnapshot.Create(db,store,owner,draft,"DRAFT",0);
    });

    public DocumentDraft FromBooking(OwnerSession owner,Booking booking,string documentId="")=>new()
    {
        Name=booking.Name,DocumentId=documentId,BookingId=booking.Id,BookingRevision=booking.Revision,
        RecipientCustomerId=booking.PrimaryCustomerId,IssueDate=DateOnly.FromDateTime(DateTime.Today)
    };

    public DocumentDraft SaveDraft(OwnerSession owner,DocumentDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id};
        return store.Write(owner,"Save quote draft",draft.Id,db=>
        {
            if(draft.Kind!=DocumentKind.Quote || draft.Status!="Draft" || draft.IssuedVersionId.Length>0
                || (draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft"))throw new ArgumentException("Issued quotes require a new draft version.");
            _=new RecordStore<Booking>(store,"Booking").Get(db,owner,draft.BookingId);
            return _drafts.Save(db,owner,draft);
        });
    }

    public DocumentVersion Issue(OwnerSession owner,string draftId,int expectedRevision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var existing=DocumentVersions.Find(db,owner,draftId:draftId);
            if(existing is not null)return existing;
            var draft=_drafts.Get(db,owner,draftId);
            if(draft.Revision!=expectedRevision || draft.Status!="Draft")throw new ArgumentException("Reload the changed draft before issuing.");
            return Issue(db,owner,draft);
        });
    }

    private DocumentVersion Issue(DbWork db,OwnerSession owner,DocumentDraft draft)
    {
        string number=PrepareRoot(db,owner,ref draft);
        int version=1+Convert.ToInt32(db.Scalar("SELECT COALESCE(MAX(Version),0) FROM DocumentVersions WHERE DocumentId=$id;",("$id",draft.DocumentId)));
        var snapshot=QuoteSnapshot.Create(db,store,owner,draft,number,version);
        var issued=DocumentVersions.Save(db,owner,draft,snapshot);
        _drafts.Save(db,owner,draft with{Status="Issued",IssuedVersionId=issued.Id});
        var book=new BookingBook(store);
        var booking=book.Records.Get(db,owner,draft.BookingId);
        if(booking.Status=="Inquiry")book.Transition(db,owner,booking,"Quoted",$"Issued {number} v{version}");
        BusinessStore.Audit(db,owner,"Issue quote",issued.Id);
        return issued;
    }

    private static string PrepareRoot(DbWork db,OwnerSession owner,ref DocumentDraft draft)
    {
        if(draft.DocumentId.Length>0)
            return db.Scalar("SELECT Number FROM Documents WHERE Id=$id AND BusinessId=$business AND Type='Quote' AND BookingId=$booking;",("$id",draft.DocumentId),("$business",owner.BusinessId),("$booking",draft.BookingId)) as string
                ??throw new ArgumentException("Choose an existing quote from this business.");
        string number=DocumentVersions.AllocateNumber(db,owner,DocumentKind.Quote);
        draft=draft with{DocumentId=Guid.NewGuid().ToString()};
        db.Execute("""
            INSERT INTO Documents (Id,BusinessId,Type,Number,BookingId,CreatedBy,CreatedAtUtc)
            VALUES ($id,$business,'Quote',$number,$booking,$owner,$at);
            """,("$id",draft.DocumentId),("$business",owner.BusinessId),("$number",number),("$booking",draft.BookingId),
            ("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        return number;
    }

    public DocumentVersion Accept(OwnerSession owner,string versionId,DateOnly acceptedDate,string evidence)=>store.Write(owner,"Accept quote",versionId,db=>
    {
        var version=DocumentVersions.Find(db,owner,id:versionId)??throw new ArgumentException("Quote not found.");
        if(version.Snapshot.Kind!=DocumentKind.Quote || version.Status!="Issued" || acceptedDate<version.Snapshot.IssueDate || string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Choose a current issued quote and record its acceptance date and evidence.");
        db.Execute("UPDATE DocumentVersions SET Status='Accepted',AcceptedDate=$date,AcceptanceEvidence=$evidence,AcceptedBy=$owner WHERE Id=$id;",
            ("$date",acceptedDate.ToString("yyyy-MM-dd")),("$evidence",evidence.Trim()),("$owner",owner.OwnerId),("$id",versionId));
        db.Execute("UPDATE DocumentVersions SET Status='Superseded' WHERE DocumentId=$document AND Id<>$id AND Status='Accepted';",("$document",version.DocumentId),("$id",versionId));
        string bookingId=(string)db.Scalar("SELECT BookingId FROM Documents WHERE Id=$id;",("$id",version.DocumentId))!;
        var book=new BookingBook(store);
        var booking=book.Records.Get(db,owner,bookingId) with{AcceptedQuoteVersionId=versionId};
        if(booking.Status is "Inquiry" or "Quoted")book.Transition(db,owner,booking,"Awaiting deposit",$"Accepted {version.Snapshot.Number} v{version.Snapshot.Version}");
        else book.Records.Save(db,owner,booking);
        return version with{Status="Accepted",AcceptedDate=acceptedDate,AcceptanceEvidence=evidence.Trim()};
    });
}
