using System.Security.Cryptography;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed record PdfArtifact(string Status,string? RelativePath,string? Sha256,string? Error);

public sealed class DocumentArtifacts(BusinessStore store)
{
    public PdfArtifact Get(OwnerSession owner,string versionId)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        _=DocumentVersions.Find(db,owner,id:versionId)??throw new ArgumentException("Document not found.");
        return Read(db,versionId);
    });

    public string Render(OwnerSession owner,string versionId)
    {
        var version=store.Read(db=>
        {
            store.RequireSession(db,owner);
            var value=DocumentVersions.Find(db,owner,id:versionId)??throw new ArgumentException("Document not found.");
            string json=(string)db.Scalar("SELECT DataJson FROM DocumentVersions WHERE Id=$id;",("$id",versionId))!;
            if(Hash(System.Text.Encoding.UTF8.GetBytes(json))!=value.Hash)throw new InvalidOperationException("The document snapshot failed its integrity check.");
            return value;
        });
        if(Get(owner,versionId).Status=="Succeeded")return VerifiedPath(owner,versionId);
        string relative=Path.Combine("Documents",version.Id,Guid.NewGuid().ToString("N")+".pdf");
        string final=SafePath(relative);
        string partial=final+".partial";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            byte[] bytes=ClientPdf.Render(version.Snapshot,version.Hash);
            File.WriteAllBytes(partial,bytes);
            return Publish(owner,version,partial,relative,Hash(bytes));
        }
        catch(Exception error){RecordFailure(owner,versionId,error.Message);throw;}
        finally{try{File.Delete(partial);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }

    private string Publish(OwnerSession owner,DocumentVersion version,string partial,string relative,string hash)=>store.Transaction(db=>
    {
        store.RequireSession(db,owner);
        if(Read(db,version.Id).Status=="Succeeded")return Verify(Read(db,version.Id));
        string final=SafePath(relative);
        File.Move(partial,final);
        int changed=db.Execute("UPDATE DocumentArtifacts SET Status='Succeeded',RelativePath=$path,Sha256=$hash,Error=NULL WHERE VersionId=$id;",
            ("$path",relative),("$hash",hash),("$id",version.Id));
        if(changed!=1)throw new InvalidOperationException("The PDF could not be recorded. Retry the pending render.");
        BusinessStore.Audit(db,owner,"Create PDF",version.Id);
        return final;
    });

    public string VerifiedPath(OwnerSession owner,string versionId)=>Verify(Get(owner,versionId));

    private string Verify(PdfArtifact artifact)
    {
        if(artifact.Status!="Succeeded" || artifact.RelativePath is null || artifact.Sha256 is null)
            throw new InvalidOperationException("Create this PDF before opening it.");
        string path=SafePath(artifact.RelativePath);
        if(!File.Exists(path) || Hash(File.ReadAllBytes(path))!=artifact.Sha256)
            throw new InvalidOperationException("The original PDF is missing or has changed. Restore its verified copy from a backup.");
        return path;
    }

    private string SafePath(string relative)
    {
        string root=Path.GetFullPath(Path.GetDirectoryName(store.DatabasePath)!)+Path.DirectorySeparatorChar;
        string path=Path.GetFullPath(Path.Combine(root,relative));
        if(Path.IsPathRooted(relative) || !path.StartsWith(root,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The document path is outside this store.");
        return path;
    }

    private static PdfArtifact Read(DbWork db,string versionId)=>db.Read(
        "SELECT Status,RelativePath,Sha256,Error FROM DocumentArtifacts WHERE VersionId=$id;",
        r=>new PdfArtifact(r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3)),
        ("$id",versionId)).SingleOrDefault()??throw new InvalidOperationException("The document's PDF record is missing.");

    private void RecordFailure(OwnerSession owner,string versionId,string error)
    {
        try{store.Write(owner,"PDF render failed",versionId,db=>db.Execute(
            "UPDATE DocumentArtifacts SET Status='Failed',Error=$error WHERE VersionId=$id AND Status<>'Succeeded';",
            ("$error",error[..Math.Min(error.Length,500)]),("$id",versionId)));}
        catch{ /* Preserve the original render/save error if recording it also fails. */ }
    }

    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}
