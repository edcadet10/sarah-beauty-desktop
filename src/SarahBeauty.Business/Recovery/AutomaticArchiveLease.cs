namespace SarahBeauty.Business.Recovery;

internal static class AutomaticArchiveLease
{
    internal static FileStream? TryAcquire(string database)
    {
        string path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database))!,"automatic-archive.lock");ArchivePaths.RejectLink(path);
        try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException error) when((error.HResult&0xffff) is 32 or 33){return null;}
    }
}
