using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal static class ArchiveReferences
{
    internal static ArchiveFileReference[] Read(DbWork db,string businessId)
    {
        var references=db.Read("SELECT A.RelativePath,A.Sha256 FROM DocumentArtifacts A JOIN DocumentVersions V ON V.Id=A.VersionId JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business AND A.Status='Succeeded';",
            r=>new ArchiveFileReference(r.GetString(0),"PDF",r.GetString(1)),("$business",businessId));
        references.AddRange(AttachmentQueries.List(db,businessId).Select(a=>new ArchiveFileReference(a.Version.RelativePath,"Attachment",a.Version.Sha256,a.Version.ByteLength)));
        if(references.Select(d=>ArchivePaths.Normalize(d.RelativePath)).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=references.Count)throw new InvalidDataException("Managed recovery file paths are duplicated.");return references.ToArray();
    }
}
