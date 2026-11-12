using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Attachments;

internal static class EvidenceLinkQueries
{
    internal static List<LinkedEvidence> List(DbWork db,OwnerSession owner,string version="",EvidenceTargetKind? kind=null,string target="")=>List(db,owner.BusinessId,version,kind,target);
    internal static List<LinkedEvidence> List(DbWork db,string businessId,string version="",EvidenceTargetKind? kind=null,string target="")=>db.Read("""
        SELECT L.Id,L.VersionId,L.TargetKind,L.TargetId,L.SnapshotJson,L.SnapshotHash,R.SnapshotJson,R.SnapshotHash FROM AttachmentLinks L
        LEFT JOIN AttachmentLinkReleases R ON R.LinkId=L.Id WHERE L.BusinessId=$business AND ($version='' OR L.VersionId=$version)
        AND ($kind=-1 OR L.TargetKind=$kind) AND ($target='' OR L.TargetId=$target) ORDER BY L.rowid;
        """,r=>Read(businessId,r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetString(3),r.GetString(4),r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.IsDBNull(7)?null:r.GetString(7)),
        ("$business",businessId),("$version",version),("$kind",kind.HasValue?(int)kind.Value:-1),("$target",target));
    internal static LinkedEvidence Get(DbWork db,OwnerSession owner,string id)=>List(db,owner).SingleOrDefault(l=>l.Link.Id==id)??throw new ArgumentException("Evidence link not found in this business.");
    internal static string? Prior(DbWork db,OwnerSession owner,bool release,string request,string payload)
    {
        string table=release?"AttachmentLinkReleases":"AttachmentLinks";
        var row=db.Read($"SELECT Id,PayloadHash FROM {table} WHERE BusinessId=$business AND RequestId=$request;",r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
        if(row.Id is not null && row.Hash!=payload)throw new ArgumentException("This request already records a different evidence decision.");return row.Id;
    }
    private static LinkedEvidence Read(string business,string id,string version,int kind,string target,string json,string hash,string? releaseJson,string? releaseHash)
    {
        var link=Verified<EvidenceLink>(json,hash);
        if(link.Id!=id || link.BusinessId!=business || link.AttachmentVersionId!=version || (int)link.Target.Kind!=kind || link.Target.Id!=target)throw new InvalidDataException("The evidence link identity does not match its retained source.");
        var release=releaseJson is null?null:Verified<EvidenceLinkRelease>(releaseJson,releaseHash!);
        if(release is not null && (release.BusinessId!=business || release.LinkId!=id || release.LinkHash!=hash))throw new InvalidDataException("The evidence release does not match its original link.");
        return new(link,hash,release,releaseHash);
    }
    private static T Verified<T>(string json,string hash)
    {
        if(AttachmentQueries.Hash(json)!=hash)throw new InvalidDataException("The evidence decision hash does not match its retained source.");
        return JsonSerializer.Deserialize<T>(json)??throw new InvalidDataException("The evidence decision is missing.");
    }
}
