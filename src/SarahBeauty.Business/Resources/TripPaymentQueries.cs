using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

internal sealed record TripPaymentState(TripPaymentAllocation[] Links,TripPaymentRelease[] Releases,TripPaymentSource[] Payments);

internal static class TripPaymentQueries
{
    internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static T ReadSnapshot<T>(string json,string hash)
    {
        if(Hash(json)!=hash)throw new InvalidDataException("A retained trip payment decision failed its content-hash check.");
        return JsonSerializer.Deserialize<T>(json)!;
    }

    internal static List<LinkedTripPayment> Links(DbWork db,OwnerSession owner,string trip="",string payment="",string id="")=>db.Read("""
        SELECT Id,ApprovedBy,AtUtc,SnapshotJson,SnapshotHash,TripId,PaymentId,Date,Cents FROM TripPaymentLinks WHERE BusinessId=$business
        AND (($trip='' AND $payment='' AND $id='') OR TripId=$trip OR PaymentId=$payment OR Id=$id) ORDER BY Date,AtUtc,Id;
        """,r=>
        {
            var snapshot=ReadSnapshot<TripPaymentPreview>(r.GetString(3),r.GetString(4));var draft=snapshot.Draft;
            if(draft.TripId!=r.GetString(5) || draft.PaymentId!=r.GetString(6) || draft.Date!=DateOnly.Parse(r.GetString(7)) || draft.Cents!=r.GetInt64(8)
                || snapshot.Trip.Id!=draft.TripId || snapshot.Payment.Id!=draft.PaymentId)throw new InvalidDataException("A trip payment link has inconsistent retained source fields.");
            return new LinkedTripPayment(r.GetString(0),r.GetString(1),r.GetString(2),snapshot,r.GetString(4));
        },("$business",owner.BusinessId),("$trip",trip),("$payment",payment),("$id",id));

    internal static List<ReleasedTripPayment> Releases(DbWork db,OwnerSession owner,string trip="",string payment="",string link="")=>db.Read("""
        SELECT R.Id,R.ApprovedBy,R.AtUtc,R.SnapshotJson,R.SnapshotHash,R.LinkId,R.Date,R.Cents FROM TripPaymentReleases R JOIN TripPaymentLinks L ON L.Id=R.LinkId
        WHERE R.BusinessId=$business AND (($trip='' AND $payment='' AND $link='') OR L.TripId=$trip OR L.PaymentId=$payment OR R.LinkId=$link) ORDER BY R.Date,R.AtUtc,R.Id;
        """,r=>
        {
            var snapshot=ReadSnapshot<TripPaymentReleasePreview>(r.GetString(3),r.GetString(4));var draft=snapshot.Draft;
            if(draft.LinkId!=r.GetString(5) || draft.Date!=DateOnly.Parse(r.GetString(6)) || draft.Cents!=r.GetInt64(7) || snapshot.Original.Id!=draft.LinkId)
                throw new InvalidDataException("A trip payment release has inconsistent retained source fields.");
            return new ReleasedTripPayment(r.GetString(0),r.GetString(1),r.GetString(2),snapshot,r.GetString(4));
        },("$business",owner.BusinessId),("$trip",trip),("$payment",payment),("$link",link));

    internal static PostedMovement Payment(DbWork db,OwnerSession owner,string id)=>db.Read("""
        SELECT M.Id,M.DataJson,B.ApprovedBy,B.AtUtc FROM Movements M JOIN PostingBatches B ON B.Id=M.Id WHERE M.BusinessId=$business AND M.Id=$id;
        """,r=>new PostedMovement(r.GetString(0),JsonSerializer.Deserialize<FinancialDraft>(r.GetString(1))!,r.GetString(2),r.GetString(3)),
        ("$business",owner.BusinessId),("$id",id)).SingleOrDefault()??throw new ArgumentException("Choose an approved payment from this business.");

    internal static TripPaymentSource Source(DbWork db,OwnerSession owner,string id)
    {
        var source=Payment(db,owner,id).Source;
        var adjustments=db.Read("""
            SELECT Id,Kind,Date,GrossCents,COALESCE(OriginalId,''),COALESCE(ReversesId,'') FROM Movements WHERE BusinessId=$business
            AND ((Kind='SupplierRefund' AND OriginalId=$id) OR ReversesId=$id OR ReversesId IN(SELECT Id FROM Movements WHERE Kind='SupplierRefund' AND OriginalId=$id));
            """,r=>new TripPaymentAdjustment(r.GetString(0),Enum.Parse<FinancialKind>(r.GetString(1)),DateOnly.Parse(r.GetString(2)),r.GetInt64(3),r.GetString(4),r.GetString(5)),("$business",owner.BusinessId),("$id",id));
        return new(id,source.Kind,source.Date!.Value,source.AmountCents,source.CashAccountId,source.EconomicOwnerId,adjustments.ToArray());
    }

    internal static TripPaymentAllocation Allocation(LinkedTripPayment link)
    {
        var snapshot=link.Snapshot;var trip=snapshot.Trip;var draft=snapshot.Draft;
        return new(link.Id,trip.Id,new(snapshot.ClaimBasisHash,trip.TripDate!.Value,trip.RequestedReimbursementCents!.Value,trip.DriverOwnerId,trip.DriverArtistId),draft.PaymentId,draft.Date,draft.Cents);
    }

    internal static TripPaymentState State(DbWork db,OwnerSession owner,string trip,string payment)
    {
        var links=Links(db,owner,trip,payment).Select(Allocation).ToArray();
        var releases=Releases(db,owner,trip,payment).Select(r=>new TripPaymentRelease(r.Id,r.Snapshot.Draft.LinkId,r.Snapshot.Draft.Date,r.Snapshot.Draft.Cents)).ToArray();
        return new(links,releases,links.Select(l=>l.PaymentId).Append(payment).Where(p=>p.Length>0).Distinct().Select(p=>Source(db,owner,p)).ToArray());
    }

    internal static long Watermark(DbWork db,OwnerSession owner)=>Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$business;",("$business",owner.BusinessId)));
}
