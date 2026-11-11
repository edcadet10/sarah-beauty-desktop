using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class CancellationCases(BusinessStore store)
{
    private readonly RecordStore<CancellationDraft> _drafts=new(store,"CancellationDraft");
    internal Action<string>? FailurePoint {get;set;}
    public List<CancellationDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);
    public ApprovedCancellation Open(OwnerSession owner,string id)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return CancellationHistory.Open(db,owner,id);});

    public CancellationDraft SaveDraft(OwnerSession owner,CancellationDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id,Name=draft.Name.Trim()};
        return store.Write(owner,"Save cancellation draft",draft.Id,db=>
        {
            if(draft.Status!="Draft" || draft.ApprovedCaseId.Length>0 || draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft")
                throw new ArgumentException("An approved cancellation decision cannot be overwritten. Record a new evidenced decision.");
            _=new BookingBook(store).Records.Get(db,owner,draft.BookingId);
            return _drafts.Save(db,owner,draft);
        });
    }

    public List<CancellationSummary> History(OwnerSession owner,string bookingId="")=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return db.Read("""
            SELECT Id,BookingId,RequestedDate,DecisionDate,json_extract(SnapshotJson,'$.Decision.Name'),ApprovedBy,AtUtc
            FROM CancellationDecisions WHERE BusinessId=$business AND ($booking='' OR BookingId=$booking) ORDER BY DecisionDate,AtUtc,Id;
            """,r=>new CancellationSummary(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),DateOnly.Parse(r.GetString(3)),r.GetString(4),r.GetString(5),r.GetString(6)),
            ("$business",owner.BusinessId),("$booking",bookingId));
    });

    public CancellationPreview Preview(OwnerSession owner,CancellationDraft draft)=>store.Transaction(db=>
    {
        store.RequireSession(db,owner);
        if(draft.Date is null)throw new ArgumentException("Choose the cancellation decision date.");
        var before=new CancellationSources(store).Load(db,owner,draft.BookingId,draft.Date.Value);
        db.Execute("SAVEPOINT CancellationPreview;");
        try{return new CancellationExecution(store,owner,db,draft).Run(before);}
        finally{db.Execute("ROLLBACK TO CancellationPreview; RELEASE CancellationPreview;");}
    });

    public ApprovedCancellation Approve(OwnerSession owner,CancellationPreview reviewed,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);string payload=CancellationHistory.Hash(JsonSerializer.Serialize(reviewed.Decision));
            var prior=db.Read("SELECT Id,PayloadHash FROM CancellationDecisions WHERE BusinessId=$business AND DraftId=$draft;",
                r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$draft",reviewed.Decision.Id)).SingleOrDefault();
            if(prior.Id is not null)
            {
                if(prior.Hash!=payload)throw new ArgumentException("This approval already belongs to a different reviewed cancellation decision.");
                return CancellationHistory.Open(db,owner,prior.Id);
            }
            var draft=_drafts.Get(db,owner,reviewed.Decision.Id);
            if(CancellationHistory.Hash(JsonSerializer.Serialize(draft))!=payload)throw new ArgumentException("The cancellation draft changed. Save and review it again.");
            var before=new CancellationSources(store).Load(db,owner,draft.BookingId,draft.Date??throw new ArgumentException("Choose the decision date."));
            if(before.Watermark!=reviewed.Before.Watermark)throw new ArgumentException("Business records changed. Refresh and review the combined decision again.");
            var snapshot=new CancellationExecution(store,owner,db,draft){FailurePoint=FailurePoint}.Run(before);
            return Save(db,owner,snapshot,payload);
        });
    }

    private ApprovedCancellation Save(DbWork db,OwnerSession owner,CancellationPreview snapshot,string payload)
    {
        var draft=snapshot.Decision;string id=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O"),json=JsonSerializer.Serialize(snapshot),hash=CancellationHistory.Hash(json);
        db.Execute("""
            INSERT INTO CancellationDecisions (Id,BusinessId,BookingId,DraftId,RequestedDate,DecisionDate,PayloadHash,SnapshotJson,SnapshotHash,ApprovedBy,AtUtc)
            VALUES ($id,$business,$booking,$draft,$requested,$date,$payload,$snapshot,$hash,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$booking",draft.BookingId),("$draft",draft.Id),("$requested",draft.RequestedDate!.Value.ToString("yyyy-MM-dd")),
            ("$date",draft.Date!.Value.ToString("yyyy-MM-dd")),("$payload",payload),("$snapshot",json),("$hash",hash),("$owner",owner.OwnerId),("$at",at));
        FailurePoint?.Invoke("case-snapshot");
        _drafts.Save(db,owner,draft with{Status="Approved",ApprovedCaseId=id});FailurePoint?.Invoke("case-draft");
        BusinessStore.Audit(db,owner,"Approve cancellation decision",id);FailurePoint?.Invoke("case-audit");
        return new(id,draft.Id,owner.OwnerId,at,snapshot,hash);
    }
}
