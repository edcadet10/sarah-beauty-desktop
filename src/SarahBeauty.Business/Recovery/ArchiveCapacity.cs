using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record ArchiveVolumeBudget(string Root,long RequiredBytes,long? AvailableBytes);
public sealed record ArchiveSpaceEstimate(long SourceBytes,long ArchiveAllowanceBytes,int ManagedFileCount,string Destination,ArchiveVolumeBudget[] Volumes)
{
    public bool HasKnownShortage=>Volumes.Any(v=>v.AvailableBytes is long free && free<v.RequiredBytes);
}
public sealed class ArchiveCapacity(BusinessStore store)
{
    internal Func<string,long?> AvailableBytes {get;set;}=FreeBytes;
    public ArchiveSpaceEstimate Estimate(OwnerSession owner,string directory)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return Read(db,owner.BusinessId,directory);});
    internal ArchiveSpaceEstimate Estimate(BackupPermit permit)=>store.ReadSnapshot(db=>{new BackupSchedules(store).Require(db,permit);return Read(db,permit.Schedule.BusinessId,permit.Schedule.Directory);});
    private ArchiveSpaceEstimate Read(DbWork db,string business,string directory)
    {
        string destination=Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));if(!Directory.Exists(destination))throw new DirectoryNotFoundException("The chosen archive folder is unavailable.");ArchivePaths.RejectLink(destination);
        string source=Path.GetDirectoryName(Path.GetFullPath(store.DatabasePath))!;var paths=new ArchivePaths(source);var references=ArchiveReferences.Read(db,business);
        if(references.Length>=100000)throw new InvalidDataException("The store exceeds the supported recovery file count.");
        long bytes=checked(Convert.ToInt64(db.Scalar("PRAGMA page_count;"))*Convert.ToInt64(db.Scalar("PRAGMA page_size;")));
        foreach(var file in references){using var input=new FileStream(paths.Resolve(file.RelativePath),FileMode.Open,FileAccess.Read,FileShare.Read);bytes=checked(bytes+input.Length);}
        if(bytes>64L*1024*1024*1024)throw new InvalidDataException("The store exceeds the supported archive size.");
        long allowance=checked(bytes+bytes/100+64L*1024*1024);return new(bytes,allowance,references.Length,destination,Volumes(source,destination,allowance));
    }
    private ArchiveVolumeBudget[] Volumes(string source,string destination,long allowance)
    {
        string sourceRoot=Path.GetPathRoot(source)!,destinationRoot=Path.GetPathRoot(destination)!;
        return string.Equals(sourceRoot,destinationRoot,StringComparison.OrdinalIgnoreCase)
            ?[new(sourceRoot,checked(allowance*3),AvailableBytes(sourceRoot))]
            :[new(sourceRoot,checked(allowance*2),AvailableBytes(sourceRoot)),new(destinationRoot,allowance,AvailableBytes(destinationRoot))];
    }
    private static long? FreeBytes(string root)
    {
        try{return new DriveInfo(root).AvailableFreeSpace;}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException){return null;}
    }
}
