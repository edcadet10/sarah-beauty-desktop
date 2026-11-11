using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed class TripPayments(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public TripPaymentPreview Preview(OwnerSession owner,TripPaymentDraft draft)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return TripPaymentRules.Preview(db,store,owner,Normalize(draft));});
    public TripPaymentReleasePreview PreviewRelease(OwnerSession owner,TripPaymentReleaseDraft draft)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return TripPaymentRules.ReleasePreview(db,owner,Normalize(draft));});
    public List<LinkedTripPayment> History(OwnerSession owner,string tripId="")=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return TripPaymentQueries.Links(db,owner,trip:tripId);});
    public List<ReleasedTripPayment> ReleaseHistory(OwnerSession owner,string tripId="")=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return TripPaymentQueries.Releases(db,owner,trip:tripId);});

    public LinkedTripPayment Approve(OwnerSession owner,TripPaymentDraft draft,long reviewedWatermark,string requestId,string phrase)
    {
        string request=Confirm(owner,requestId,phrase);draft=Normalize(draft);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);string payload=TripPaymentQueries.Hash(JsonSerializer.Serialize(draft));
            var prior=Prior(db,owner,"TripPaymentLinks",request,payload);
            if(prior is not null)return TripPaymentQueries.Links(db,owner,id:prior).Single();
            var preview=TripPaymentRules.Preview(db,store,owner,draft);
            if(preview.Watermark!=reviewedWatermark)throw new ArgumentException("Business records changed. Refresh and review the trip payment link again.");
            return TripPaymentWriter.Link(db,owner,preview,request,payload,FailurePoint);
        });
    }

    public ReleasedTripPayment Release(OwnerSession owner,TripPaymentReleaseDraft draft,long reviewedWatermark,string requestId,string phrase)
    {
        string request=Confirm(owner,requestId,phrase);draft=Normalize(draft);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);string payload=TripPaymentQueries.Hash(JsonSerializer.Serialize(draft));
            var prior=Prior(db,owner,"TripPaymentReleases",request,payload);
            if(prior is not null)return TripPaymentQueries.Releases(db,owner).Single(r=>r.Id==prior);
            var preview=TripPaymentRules.ReleasePreview(db,owner,draft);
            if(preview.Watermark!=reviewedWatermark)throw new ArgumentException("Business records changed. Refresh and review the trip payment release again.");
            return TripPaymentWriter.Release(db,owner,preview,request,payload,FailurePoint);
        });
    }

    private static string? Prior(DbWork db,OwnerSession owner,string table,string request,string payload)
    {
        // Both callers provide fixed table names; owner and request values stay parameters.
        var prior=db.Read($"SELECT Id,PayloadHash FROM {table} WHERE BusinessId=$business AND RequestId=$request;",r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
        if(prior.Id is not null && prior.Hash!=payload)throw new ArgumentException("This request already belongs to a different trip payment decision.");
        return prior.Id;
    }
    private string Confirm(OwnerSession owner,string request,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return Guid.TryParse(request,out var id)?id.ToString():throw new ArgumentException("Use a permanent request ID for this reviewed trip payment action.");
    }
    private static TripPaymentDraft Normalize(TripPaymentDraft draft)=>draft with{PayeeReview=draft.PayeeReview.Trim(),EvidenceId=draft.EvidenceId.Trim()};
    private static TripPaymentReleaseDraft Normalize(TripPaymentReleaseDraft draft)=>draft with{Reason=draft.Reason.Trim(),EvidenceId=draft.EvidenceId.Trim()};
}
