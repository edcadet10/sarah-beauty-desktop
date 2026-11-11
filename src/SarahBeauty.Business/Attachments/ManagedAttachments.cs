using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Attachments;

public sealed class ManagedAttachments(BusinessStore store)
{
    private readonly AttachmentPaths _paths=new(store.DatabasePath);
    internal Action<string>? FailurePoint {get;set;}
    public List<StoredAttachment> History(OwnerSession owner,string attachmentId="")=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return AttachmentQueries.List(db,owner,attachmentId);});
    public StoredAttachment Get(OwnerSession owner,string versionId)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return AttachmentQueries.Get(db,owner,versionId);});

    public StoredAttachment Import(OwnerSession owner,string source,AttachmentImport draft,string requestId,CancellationToken cancellation=default)
    {
        if(!Guid.TryParse(requestId,out var request))throw new ArgumentException("Use a permanent request ID for this import.");draft=AttachmentRules.Normalize(draft);
        store.Read(db=>{store.RequireSession(db,owner);return AttachmentRules.Permission(db,owner,draft);});
        string id=Guid.NewGuid().ToString("N");var staged=AttachmentCopy.Stage(_paths,source,id,FailurePoint,cancellation);bool completed=false;
        try
        {
            var result=store.Transaction(db=>Publish(db,owner,draft,staged,id,request.ToString(),cancellation));completed=true;return result;
        }
        finally
        {
            _paths.DeletePartial(staged.PartialPath);
            if(!completed)DeleteUnrecorded(owner,id,staged.RelativePath);
        }
    }

    private StoredAttachment Publish(DbWork db,OwnerSession owner,AttachmentImport draft,StagedAttachment staged,string id,string request,CancellationToken cancellation)
    {
        store.RequireSession(db,owner);cancellation.ThrowIfCancellationRequested();
        string payload=AttachmentQueries.Hash(JsonSerializer.Serialize(new{Draft=draft,staged.OriginalFileName,staged.ByteLength,staged.Sha256}));
        var prior=AttachmentQueries.Prior(db,owner,request,payload);if(prior is not null){AttachmentRules.MayRead(db,owner,prior.Version);using var verified=Open(prior.Version);return prior;}
        string permission=AttachmentRules.Permission(db,owner,draft);var previous=draft.AttachmentId.Length==0?null:AttachmentQueries.List(db,owner,draft.AttachmentId).LastOrDefault()?.Version;
        AttachmentRules.Replacement(draft,previous);
        if(draft.Category=="Customer photo" && !staged.MediaType.StartsWith("image/",StringComparison.Ordinal))throw new ArgumentException("A customer photo must be a JPEG or PNG image.");
        var version=new AttachmentVersion(id,owner.BusinessId,previous?.AttachmentId??Guid.NewGuid().ToString(),checked((previous?.Number??0)+1),previous?.Id??"",draft.Name,draft.Category,
            staged.OriginalFileName,staged.MediaType,staged.RelativePath,staged.ByteLength,staged.Sha256,draft.CustomerId,permission,draft.PermissionUse,draft.Reason,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        File.Move(_paths.Resolve(staged.PartialPath),_paths.Resolve(staged.RelativePath));FailurePoint?.Invoke("File published");
        string json=JsonSerializer.Serialize(version),hash=AttachmentQueries.Hash(json);
        db.Execute("""
            INSERT INTO AttachmentVersions (Id,BusinessId,AttachmentId,Number,RequestId,PayloadHash,SnapshotJson,SnapshotHash,CreatedBy,AtUtc)
            VALUES ($id,$business,$attachment,$number,$request,$payload,$json,$hash,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$attachment",version.AttachmentId),("$number",version.Number),("$request",request),("$payload",payload),("$json",json),("$hash",hash),("$owner",owner.OwnerId),("$at",version.AtUtc));
        FailurePoint?.Invoke("Metadata written");BusinessStore.Audit(db,owner,"Import managed attachment version",id);return new(version,hash);
    }

    public Stream OpenRead(OwnerSession owner,string versionId)
    {
        FileStream? stream=null;
        try{return store.ReadSnapshot(db=>{store.RequireSession(db,owner);var version=AttachmentQueries.Get(db,owner,versionId).Version;AttachmentRules.MayRead(db,owner,version);return stream=Open(version);});}
        catch{stream?.Dispose();throw;}
    }
    private FileStream Open(AttachmentVersion version)
    {
        FileStream stream;
        try{stream=new FileStream(_paths.Resolve(version.RelativePath),FileMode.Open,FileAccess.Read,FileShare.Read);}
        catch(FileNotFoundException error){throw new InvalidDataException("The managed file is missing. Restore its verified copy from a complete backup.",error);}
        catch(DirectoryNotFoundException error){throw new InvalidDataException("The managed file folder is missing. Restore its verified copy from a complete backup.",error);}
        try
        {
            if(stream.Length!=version.ByteLength || Convert.ToHexString(SHA256.HashData(stream))!=version.Sha256)throw new InvalidDataException("The managed file is missing or changed. Restore its verified copy from a complete backup.");
            stream.Position=0;return stream;
        }
        catch{stream.Dispose();throw;}
    }
    private void DeleteUnrecorded(OwnerSession owner,string id,string relative)
    {
        // If database state cannot be read after an interrupted commit, retain the file rather than risk deleting a committed version.
        try{if(store.Read(db=>AttachmentQueries.List(db,owner,versionId:id).Count)==0)_paths.DeletePartial(relative);}catch{}
    }
}
