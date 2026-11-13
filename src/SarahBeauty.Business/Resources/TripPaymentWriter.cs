using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

internal static class TripPaymentWriter
{
    internal static LinkedTripPayment Link(DbWork db,OwnerSession owner,TripPaymentPreview snapshot,string request,string payload,Action<string>? failure)
    {
        string id=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O"),json=JsonSerializer.Serialize(snapshot),hash=TripPaymentQueries.Hash(json);var draft=snapshot.Draft;
        db.Execute("""
            INSERT INTO TripPaymentLinks (Id,BusinessId,TripId,PaymentId,Date,Cents,RequestId,PayloadHash,SnapshotJson,SnapshotHash,ApprovedBy,AtUtc)
            VALUES ($id,$business,$trip,$payment,$date,$cents,$request,$payload,$snapshot,$hash,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$trip",draft.TripId),("$payment",draft.PaymentId),("$date",draft.Date.ToStorageDate()),("$cents",draft.Cents),
            ("$request",request),("$payload",payload),("$snapshot",json),("$hash",hash),("$owner",owner.OwnerId),("$at",at));
        failure?.Invoke("trip-link");BusinessStore.Audit(db,owner,"Link actual trip reimbursement payment",id);failure?.Invoke("audit");
        return new(id,owner.OwnerId,at,snapshot,hash);
    }

    internal static ReleasedTripPayment Release(DbWork db,OwnerSession owner,TripPaymentReleasePreview snapshot,string request,string payload,Action<string>? failure)
    {
        string id=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O"),json=JsonSerializer.Serialize(snapshot),hash=TripPaymentQueries.Hash(json);var draft=snapshot.Draft;
        db.Execute("""
            INSERT INTO TripPaymentReleases (Id,BusinessId,LinkId,Date,Cents,RequestId,PayloadHash,SnapshotJson,SnapshotHash,ApprovedBy,AtUtc)
            VALUES ($id,$business,$link,$date,$cents,$request,$payload,$snapshot,$hash,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$link",draft.LinkId),("$date",draft.Date.ToStorageDate()),("$cents",draft.Cents),
            ("$request",request),("$payload",payload),("$snapshot",json),("$hash",hash),("$owner",owner.OwnerId),("$at",at));
        failure?.Invoke("trip-release");BusinessStore.Audit(db,owner,"Release trip reimbursement payment link",id);failure?.Invoke("audit");
        return new(id,owner.OwnerId,at,snapshot,hash);
    }
}
