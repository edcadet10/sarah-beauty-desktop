using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BookPeriodQueries
{
    internal static ClosedBookPeriod Open(DbWork db,OwnerSession owner,string id)=>db.Read("""
        SELECT C.Id,C.ApprovedBy,C.AtUtc,C.SnapshotJson,C.SnapshotHash,EXISTS(SELECT 1 FROM BookPeriodReopens R WHERE R.PeriodId=C.Id)
        FROM BookPeriodCloses C WHERE C.BusinessId=$business AND C.Id=$id;
        """,r=>
        {
            string json=r.GetString(3),hash=r.GetString(4);
            if(Hash(json)!=hash)throw new InvalidDataException("A retained period report failed its content-hash check.");
            return new ClosedBookPeriod(r.GetString(0),r.GetString(1),r.GetString(2),JsonSerializer.Deserialize<BookPeriodPreview>(json)!,hash,r.GetInt32(5)==1);
        },("$business",owner.BusinessId),("$id",id)).SingleOrDefault()??throw new ArgumentException("Choose a retained book period from this business.");

    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
