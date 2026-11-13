using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class BookPeriods(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public DateOnly? ClosedThrough(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return BookPeriodRules.ClosedThrough(db,owner);});
    public BookPeriodPreview Preview(OwnerSession owner,BookPeriodDraft draft)=>store.ReadSnapshot(db=>
    {store.RequireSession(db,owner);return BookPeriodRules.Preview(db,store,owner,Normalize(draft));});
    public ClosedBookPeriod Open(OwnerSession owner,string id)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return BookPeriodQueries.Open(db,owner,id);});

    public List<BookPeriodReopen> ReopenHistory(OwnerSession owner,string id)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return db.Read("""
            SELECT R.Id,R.PeriodId,R.Reason,R.OwnerId,R.AtUtc FROM BookPeriodReopens R JOIN BookPeriodCloses C ON C.Id=R.PeriodId
            WHERE C.BusinessId=$business AND C.Id=$id ORDER BY R.AtUtc;
            """,r=>new BookPeriodReopen(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)),("$business",owner.BusinessId),("$id",id));
    });

    public List<BookPeriodSummary> History(OwnerSession owner)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return db.Read("""
            SELECT C.Id,C.FromDate,C.ThroughDate,C.ApprovedBy,C.AtUtc,EXISTS(SELECT 1 FROM BookPeriodReopens R WHERE R.PeriodId=C.Id)
            FROM BookPeriodCloses C WHERE C.BusinessId=$business ORDER BY C.ThroughDate,C.AtUtc,C.Id;
            """,r=>new BookPeriodSummary(r.GetString(0),StoredDates.Parse(r.GetString(1)),StoredDates.Parse(r.GetString(2)),r.GetString(3),r.GetString(4),r.GetInt32(5)==1),("$business",owner.BusinessId));
    });

    public ClosedBookPeriod Close(OwnerSession owner,BookPeriodDraft draft,long reviewedWatermark,string requestId,string phrase)
    {
        string request=Confirm(owner,requestId,phrase);draft=Normalize(draft);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            string payload=BookPeriodQueries.Hash(JsonSerializer.Serialize(draft));
            var prior=db.Read("SELECT Id,PayloadHash FROM BookPeriodCloses WHERE BusinessId=$business AND RequestId=$request;",r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
            if(prior.Id is not null)
            {
                if(prior.Hash!=payload)throw new ArgumentException("This close request already belongs to a different reviewed decision.");
                return BookPeriodQueries.Open(db,owner,prior.Id);
            }
            var preview=BookPeriodRules.Preview(db,store,owner,draft);
            if(preview.Reports.Watermark!=reviewedWatermark)throw new ArgumentException("Business records changed. Refresh and review the period reports again.");
            if(!preview.ReadyToClose)throw new ArgumentException("Resolve every listed opening, reconciliation, date, and report issue before closing this period.");
            return Save(db,owner,preview,request,payload);
        });
    }

    private ClosedBookPeriod Save(DbWork db,OwnerSession owner,BookPeriodPreview preview,string request,string payload)
    {
        string id=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O"),json=JsonSerializer.Serialize(preview),hash=BookPeriodQueries.Hash(json);
        db.Execute("""
            INSERT INTO BookPeriodCloses (Id,BusinessId,FromDate,ThroughDate,RequestId,PayloadHash,SnapshotJson,SnapshotHash,ApprovedBy,AtUtc)
            VALUES ($id,$business,$from,$through,$request,$payload,$snapshot,$hash,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$from",preview.Draft.From.ToStorageDate()),("$through",preview.Draft.Through.ToStorageDate()),
            ("$request",request),("$payload",payload),("$snapshot",json),("$hash",hash),("$owner",owner.OwnerId),("$at",at));
        FailurePoint?.Invoke("period-snapshot");
        db.Execute("INSERT INTO BookSettings (BusinessId,ClosedThrough) VALUES ($business,$date) ON CONFLICT(BusinessId) DO UPDATE SET ClosedThrough=excluded.ClosedThrough;",
            ("$business",owner.BusinessId),("$date",preview.Draft.Through.ToStorageDate()));
        FailurePoint?.Invoke("period-lock");
        BusinessStore.Audit(db,owner,"Close book period",id);FailurePoint?.Invoke("audit");
        return new(id,owner.OwnerId,at,preview,hash,false);
    }

    public BookPeriodReopen Reopen(OwnerSession owner,string id,string reason,string requestId,string phrase)
    {
        string request=Confirm(owner,requestId,phrase);reason=reason.Trim();
        if(reason.Length==0)throw new ArgumentException("Record the reason for reopening this book period.");
        return store.Transaction(db=>{store.RequireSession(db,owner);return BookPeriodReopening.Reopen(db,owner,id,reason,request,FailurePoint);});
    }

    private string Confirm(OwnerSession owner,string request,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return Guid.TryParse(request,out var id)?id.ToString():throw new ArgumentException("Use a permanent request ID for this reviewed period action.");
    }
    private static BookPeriodDraft Normalize(BookPeriodDraft draft)=>draft with{Reason=draft.Reason.Trim(),EvidenceId=draft.EvidenceId.Trim()};
}
