namespace SarahBeauty.Business.Attachments;

public sealed record ManagedEvidenceFile(string VersionId,string MetadataHash,string Name,int Number,string Category,string OriginalFileName,string MediaType,long ByteLength,string Sha256)
{
    public static ManagedEvidenceFile From(StoredAttachment file)=>new(file.Version.Id,file.SnapshotHash,file.Version.Name,file.Version.Number,file.Version.Category,
        file.Version.OriginalFileName,file.Version.MediaType,file.Version.ByteLength,file.Version.Sha256);
}
