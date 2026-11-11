using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed class TripReimbursementSources(BusinessStore store)
{
    internal Action? AfterTripRead {get;set;}
    public TripReimbursementReview Load(OwnerSession owner,string tripId,DateOnly asOf)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);var trip=new RecordStore<MileageTrip>(store,"MileageTrip").Get(db,owner,tripId);AfterTripRead?.Invoke();
        var links=TripPaymentQueries.Links(db,owner,trip:trip.Id);var releases=TripPaymentQueries.Releases(db,owner,trip:trip.Id).ToArray();
        var released=releases.Select(r=>new TripPaymentRelease(r.Id,r.Snapshot.Draft.LinkId,r.Snapshot.Draft.Date,r.Snapshot.Draft.Cents)).ToArray();
        string basis=TripPaymentRules.Basis(trip).Hash;
        var rows=links.Select(l=>
        {
            var allocation=TripPaymentQueries.Allocation(l);
            return new TripReimbursementRow(l,(long)TripPaymentAmounts.Remaining(allocation,released,asOf),
                (long)TripPaymentAmounts.Remaining(allocation,released,DateOnly.MaxValue),l.Snapshot.ClaimBasisHash==basis);
        }).ToArray();
        return new TripReimbursementReview((string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$business;",("$business",owner.BusinessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,TripPaymentQueries.Watermark(db,owner),DateTimeOffset.UtcNow.ToString("O"),asOf,trip,
            rows.Where(r=>r.CurrentBasis).Sum(r=>r.RemainingCents),rows.Where(r=>!r.CurrentBasis).Sum(r=>r.RemainingCents),rows,releases);
    });
}
