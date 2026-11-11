namespace SarahBeauty.Business.Attachments;

public sealed record AttachmentImport(string Name,string Category,string Reason,string CustomerId="",string PermissionUse="",string AttachmentId="",int ExpectedVersion=0);
public sealed record AttachmentVersion(string Id,string BusinessId,string AttachmentId,int Number,string PreviousVersionId,string Name,string Category,
    string OriginalFileName,string MediaType,string RelativePath,long ByteLength,string Sha256,string CustomerId,string PermissionId,string PermissionUse,
    string Reason,string CreatedBy,string AtUtc);
public sealed record StoredAttachment(AttachmentVersion Version,string SnapshotHash);
internal sealed record StagedAttachment(string PartialPath,string RelativePath,string OriginalFileName,string MediaType,long ByteLength,string Sha256);
