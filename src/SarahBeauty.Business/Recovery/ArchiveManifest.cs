namespace SarahBeauty.Business.Recovery;

public sealed record ArchiveFile(string RelativePath,string Kind,long ByteLength,string Sha256);
public sealed record ArchiveTableCount(string Table,long Rows);
public sealed record ArchiveDatabaseFacts(ArchiveTableCount[] Tables,long MovementCount,long JournalLineCount,string TotalDebits,string TotalCredits);
public sealed record ArchiveManifest(int Version,string BusinessId,string BusinessName,string StoreId,string Mode,int SchemaVersion,long Watermark,string CreatedAtUtc,string AppVersion,
    ArchiveFile[] Files,ArchiveDatabaseFacts Facts);
internal sealed record ArchiveFileReference(string RelativePath,string Kind,string ExpectedHash,long? ExpectedBytes=null);
