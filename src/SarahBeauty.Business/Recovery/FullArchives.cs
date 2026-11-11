using System.Security.Cryptography;
using System.Text;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record CreatedArchive(ArchiveManifest Manifest,long ByteLength,string Sha256,string ManifestHash,string RecipientFingerprint);
public sealed class FullArchives(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public async Task<CreatedArchive> CreateAsync(OwnerSession owner,string destination,string recipient,CancellationToken cancellation=default)
    {
        store.Read(db=>{store.RequireSession(db,owner);return true;});return await Create(destination,recipient,()=>new ArchiveSources(store){FailurePoint=FailurePoint}.Capture(owner,cancellation),null,cancellation);
    }
    internal async Task<CreatedArchive> CreateAutomaticAsync(BackupPermit permit,string destination,CancellationToken cancellation=default)
    {
        void Authorize()=>store.Read(db=>{new BackupSchedules(store).Require(db,permit);return true;});Authorize();
        if(!string.Equals(Path.GetDirectoryName(Path.GetFullPath(destination)),permit.Schedule.Directory,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Automatic archives must use their confirmed private folder.");
        return await Create(destination,permit.Recipient,()=>new ArchiveSources(store){FailurePoint=FailurePoint}.Capture(permit,cancellation),Authorize,cancellation);
    }
    private async Task<CreatedArchive> Create(string destination,string recipient,Func<ArchiveCapture> capture,Action? authorizePublication,CancellationToken cancellation)
    {
        var cipher=new RecoveryCipher();string reviewed=await cipher.InspectRecipientAsync(recipient,cancellation);using var target=new RecoveryOutputStage(destination);
        using var captured=await Task.Run(capture,cancellation);var package=await Task.Run(()=>ArchivePackage.Write(captured,cancellation),cancellation);FailurePoint?.Invoke("Package created");
        using var held=new FileStream(package.Path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(held.Length!=package.ByteLength || Convert.ToHexString(await SHA256.HashDataAsync(held,cancellation))!=package.PackageHash)throw new InvalidDataException("The prepared recovery package changed before encryption.");
        var encrypted=await cipher.EncryptAsync(package.Path,target.Path,reviewed,cancellation);FailurePoint?.Invoke("Package encrypted");
        authorizePublication?.Invoke();
        var result=await target.Publish(new(){Ok=true,Bytes=encrypted.ByteLength,Sha256=encrypted.Sha256},cancellation);
        return new(captured.Manifest,result.ByteLength,result.Sha256,package.ManifestHash,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reviewed))));
    }
}
