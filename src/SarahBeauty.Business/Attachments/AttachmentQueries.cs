using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Attachments;

internal static class AttachmentQueries
{
    internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static List<StoredAttachment> List(DbWork db,OwnerSession owner,string attachmentId="",string versionId="")=>db.Read("""
        SELECT Id,BusinessId,AttachmentId,Number,SnapshotJson,SnapshotHash FROM AttachmentVersions WHERE BusinessId=$business
        AND ($attachment='' OR AttachmentId=$attachment) AND ($version='' OR Id=$version) ORDER BY AttachmentId,Number;
        """,r=>Read(r.GetString(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetString(4),r.GetString(5)),
        ("$business",owner.BusinessId),("$attachment",attachmentId),("$version",versionId));
    internal static StoredAttachment Get(DbWork db,OwnerSession owner,string id)=>List(db,owner,versionId:id).SingleOrDefault()??throw new ArgumentException("Attachment version not found in this business.");
    internal static StoredAttachment? Prior(DbWork db,OwnerSession owner,string request,string payload)
    {
        var row=db.Read("SELECT Id,PayloadHash FROM AttachmentVersions WHERE BusinessId=$business AND RequestId=$request;",
            r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
        if(row.Id is null)return null;
        if(row.Hash!=payload)throw new ArgumentException("This request already imported different file content or metadata. Use a new request for a separate import.");
        return Get(db,owner,row.Id);
    }
    private static StoredAttachment Read(string id,string business,string attachment,int number,string json,string hash)
    {
        if(Hash(json)!=hash)throw new InvalidDataException("The attachment metadata hash does not match its retained source.");
        var value=JsonSerializer.Deserialize<AttachmentVersion>(json)??throw new InvalidDataException("The attachment metadata is missing.");
        if(value.Id!=id || value.BusinessId!=business || value.AttachmentId!=attachment || value.Number!=number)throw new InvalidDataException("The attachment identity does not match its retained source.");
        return new(value,hash);
    }
}
