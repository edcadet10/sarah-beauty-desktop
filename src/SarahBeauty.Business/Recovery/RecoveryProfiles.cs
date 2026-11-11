using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record RecoveryProfile(int Version,string BusinessId,string Recipient,string RecipientFingerprint,string RecoveryFileHash,string ConfirmedBy,string ConfirmedAtUtc);
public sealed class RecoveryProfiles(BusinessStore store)
{
    internal static string Key(string businessId)=>"RecoveryProfile/"+businessId;
    public RecoveryProfile? Current(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return Read(db,owner.BusinessId);});
    public async Task<RecoveryProfile> RegisterAsync(OwnerSession owner,byte[] protectedIdentity,string passphrase,CancellationToken cancellation=default)
    {
        store.Read(db=>{store.RequireSession(db,owner);return true;});if(protectedIdentity.Length is <1 or >65536)throw new ArgumentException("Choose a supported protected recovery file.");byte[] reviewed=protectedIdentity.ToArray();
        string recipient=await new RecoveryCipher().InspectAsync(reviewed,passphrase,cancellation);cancellation.ThrowIfCancellationRequested();
        var profile=new RecoveryProfile(1,owner.BusinessId,recipient,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recipient))),Convert.ToHexString(SHA256.HashData(reviewed)),owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);var prior=Read(db,owner.BusinessId);if(prior?.Recipient==recipient && prior.RecoveryFileHash==profile.RecoveryFileHash)return prior;
            db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(owner.BusinessId)),("$json",JsonSerializer.Serialize(profile)));
            BusinessStore.Audit(db,owner,"Confirmed portable recovery material",null);return profile;
        });
    }
    internal static RecoveryProfile? Read(DbWork db,string businessId)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(businessId))) as string;if(json is null)return null;
        var profile=JsonSerializer.Deserialize<RecoveryProfile>(json)??throw new InvalidDataException("The recovery configuration is incomplete.");
        if(profile.Version!=1 || profile.BusinessId!=businessId || !ArchiveManifestChecks.Hash(profile.RecoveryFileHash) || profile.Recipient is null || Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.Recipient)))!=profile.RecipientFingerprint)throw new InvalidDataException("The recovery configuration needs review.");return profile;
    }
}
