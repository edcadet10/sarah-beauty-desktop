using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Security;

internal static class OwnerCredentials
{
    internal static string Stamp(string salt,string hash,int iterations)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Salt=salt,Hash=hash,Iterations=iterations})));
    internal static void Require(DbWork db,OwnerSession session)
    {
        string? current=db.Read("SELECT Salt,Hash,Iterations FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
            r=>Stamp(r.GetString(0),r.GetString(1),r.GetInt32(2)),("$id",session.OwnerId),("$business",session.BusinessId)).SingleOrDefault();
        if(current is not null && session.CredentialStamp==current)return;
        session.Lock();throw new UnauthorizedAccessException("Owner credentials or access changed. Sign in again.");
    }
}
