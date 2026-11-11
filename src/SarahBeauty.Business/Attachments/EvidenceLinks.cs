using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Attachments;

public sealed class EvidenceLinks(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public List<LinkedEvidence> History(OwnerSession owner,string versionId="",EvidenceTargetKind? kind=null,string targetId="")=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);if(kind.HasValue && !Enum.IsDefined(kind.Value) || targetId.Length>0 && kind is null)throw new ArgumentException("Choose the record type for an evidence target.");
        return EvidenceLinkQueries.List(db,owner,versionId,kind,targetId);
    });

    public Stream OpenRead(OwnerSession owner,string linkId)
    {
        FileStream? stream=null;
        try{return store.ReadSnapshot(db=>
        {
            store.RequireSession(db,owner);var link=EvidenceLinkQueries.Get(db,owner,linkId).Link;var file=AttachmentQueries.Get(db,owner,link.AttachmentVersionId);
            if(file.SnapshotHash!=link.AttachmentSnapshotHash)throw new InvalidDataException("The file version no longer matches the original evidence link.");
            AttachmentRules.MayRead(db,owner,file.Version);return stream=new ManagedAttachments(store).OpenVerified(file.Version);
        });}
        catch{stream?.Dispose();throw;}
    }

    public LinkedEvidence Link(OwnerSession owner,EvidenceLinkDraft draft,string requestId)
    {
        string request=Request(requestId);draft=draft with{Purpose=draft.Purpose.Trim()};if(draft.Purpose.Length is <1 or >1000)throw new ArgumentException("Enter the reason/purpose for this evidence link.");
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);string payload=AttachmentQueries.Hash(JsonSerializer.Serialize(draft));string? prior=EvidenceLinkQueries.Prior(db,owner,false,request,payload);
            if(prior is not null)return EvidenceLinkQueries.Get(db,owner,prior);
            var file=AttachmentQueries.Get(db,owner,draft.AttachmentVersionId);AttachmentRules.MayRead(db,owner,file.Version);
            var target=EvidenceTargets.Get(db,owner,draft.TargetKind,draft.TargetId);
            if(target.Revision!=draft.ExpectedTargetRevision)throw new InvalidOperationException("The target record changed. Refresh and review its current version before linking evidence.");
            if(file.Version.Category=="Customer photo" && (target.Kind!=EvidenceTargetKind.Customer || target.Id!=file.Version.CustomerId))throw new ArgumentException("Link a customer photo only to its own permission-reviewed customer record.");
            if(EvidenceLinkQueries.List(db,owner,file.Version.Id,target.Kind,target.Id).Any(l=>l.Release is null))throw new ArgumentException("This file version already has an active link to that record.");
            using var verified=new ManagedAttachments(store).OpenVerified(file.Version);
            var link=new EvidenceLink(Guid.NewGuid().ToString(),owner.BusinessId,file.Version.Id,file.SnapshotHash,target,draft.Purpose,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            string json=JsonSerializer.Serialize(link),hash=AttachmentQueries.Hash(json);WriteLink(db,owner,link,request,payload,json,hash);
            FailurePoint?.Invoke("Link written");BusinessStore.Audit(db,owner,"Link managed evidence version",link.Id);return new LinkedEvidence(link,hash,null,null);
        });
    }

    public LinkedEvidence Release(OwnerSession owner,string linkId,string reason,string requestId)
    {
        string request=Request(requestId);reason=reason.Trim();if(reason.Length is <1 or >1000)throw new ArgumentException("Enter a reason for releasing this evidence link.");
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);string payload=AttachmentQueries.Hash(JsonSerializer.Serialize(new{LinkId=linkId,Reason=reason}));
            string? prior=EvidenceLinkQueries.Prior(db,owner,true,request,payload);var original=EvidenceLinkQueries.Get(db,owner,linkId);
            if(prior is not null){if(original.Release?.Id!=prior)throw new InvalidDataException("The original release no longer matches this request.");return original;}
            if(original.Release is not null)throw new ArgumentException("This evidence link was already released. Its history is retained.");
            var release=new EvidenceLinkRelease(Guid.NewGuid().ToString(),owner.BusinessId,linkId,original.SnapshotHash,reason,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            string json=JsonSerializer.Serialize(release),hash=AttachmentQueries.Hash(json);WriteRelease(db,owner,release,request,payload,json,hash);
            FailurePoint?.Invoke("Release written");BusinessStore.Audit(db,owner,"Release managed evidence link",release.Id);return original with{Release=release,ReleaseHash=hash};
        });
    }

    private static string Request(string value)=>Guid.TryParse(value,out var id)?id.ToString():throw new ArgumentException("Use a permanent request ID for this evidence decision.");
    private static void WriteLink(DbWork db,OwnerSession owner,EvidenceLink link,string request,string payload,string json,string hash)=>db.Execute("""
        INSERT INTO AttachmentLinks (Id,BusinessId,VersionId,TargetKind,TargetId,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,AtUtc)
        VALUES ($id,$business,$version,$kind,$target,$request,$payload,$json,$hash,$owner,$at);
        """,("$id",link.Id),("$business",owner.BusinessId),("$version",link.AttachmentVersionId),("$kind",(int)link.Target.Kind),("$target",link.Target.Id),("$request",request),("$payload",payload),("$json",json),("$hash",hash),("$owner",owner.OwnerId),("$at",link.AtUtc));
    private static void WriteRelease(DbWork db,OwnerSession owner,EvidenceLinkRelease release,string request,string payload,string json,string hash)=>db.Execute("""
        INSERT INTO AttachmentLinkReleases (Id,BusinessId,LinkId,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,AtUtc)
        VALUES ($id,$business,$link,$request,$payload,$json,$hash,$owner,$at);
        """,("$id",release.Id),("$business",owner.BusinessId),("$link",release.LinkId),("$request",request),("$payload",payload),("$json",json),("$hash",hash),("$owner",owner.OwnerId),("$at",release.AtUtc));
}
