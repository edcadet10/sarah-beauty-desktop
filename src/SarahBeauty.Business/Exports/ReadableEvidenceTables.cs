using SarahBeauty.Business.Attachments;

namespace SarahBeauty.Business.Exports;

internal static class ReadableEvidenceTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var versions=new ReadableTableBuilder(source,budget,"Managed evidence version references","VersionId","BusinessId","AttachmentId","VersionNumber","PreviousVersionId","Category","MediaType","ManagedRelativePath","ByteLength","FileSha256","CustomerId","PermissionId","CreatedByOwnerId","CreatedAtUtc","SnapshotSha256");
        foreach(var item in AttachmentQueries.List(db,source.BusinessId))
        {
            var value=item.Version;
            versions.Add(value.Id,value.BusinessId,value.AttachmentId,value.Number,value.PreviousVersionId,value.Category,value.MediaType,value.RelativePath,value.ByteLength,value.Sha256,value.CustomerId,value.PermissionId,value.CreatedBy,value.AtUtc,item.SnapshotHash);
        }
        var links=new ReadableTableBuilder(source,budget,"Managed evidence links","LinkId","BusinessId","VersionId","AttachmentSnapshotSha256","TargetKind","TargetId","TargetRevision","TargetStatusAtLink","TargetArchivedAtLink","SnapshotSha256","OwnerId","AtUtc","Released");
        var releases=new ReadableTableBuilder(source,budget,"Managed evidence link releases","ReleaseId","BusinessId","LinkId","LinkSnapshotSha256","SnapshotSha256","OwnerId","AtUtc");
        foreach(var item in EvidenceLinkQueries.List(db,source.BusinessId))
        {
            var value=item.Link;var target=value.Target;
            links.Add(value.Id,value.BusinessId,value.AttachmentVersionId,value.AttachmentSnapshotHash,target.Kind,target.Id,target.Revision,target.Status,target.Archived,item.SnapshotHash,value.OwnerId,value.AtUtc,item.Release is not null);
            if(item.Release is {} release)releases.Add(release.Id,release.BusinessId,release.LinkId,release.LinkHash,item.ReleaseHash,release.OwnerId,release.AtUtc);
        }
        return new[]{versions.Freeze(),links.Freeze(),releases.Freeze()};
    });
}
