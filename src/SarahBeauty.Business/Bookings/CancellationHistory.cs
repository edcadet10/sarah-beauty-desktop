using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed record ApprovedCancellation(string Id,string DraftId,string ApprovedBy,string AtUtc,CancellationPreview Snapshot,string SnapshotHash);
public sealed record CancellationSummary(string Id,string BookingId,DateOnly RequestedDate,DateOnly DecisionDate,string Reason,string ApprovedBy,string AtUtc);

internal static class CancellationHistory
{
    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static ApprovedCancellation Open(DbWork db,OwnerSession owner,string id)=>db.Read("""
        SELECT Id,DraftId,ApprovedBy,AtUtc,SnapshotJson,SnapshotHash FROM CancellationDecisions WHERE BusinessId=$business AND Id=$id;
        """,r=>
        {
            string json=r.GetString(4),hash=r.GetString(5);
            if(Hash(json)!=hash)throw new InvalidDataException("The retained cancellation decision failed its content-hash check.");
            return new ApprovedCancellation(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),JsonSerializer.Deserialize<CancellationPreview>(json)!,hash);
        },("$business",owner.BusinessId),("$id",id)).SingleOrDefault()??throw new ArgumentException("Choose an approved cancellation decision from this business.");
}
