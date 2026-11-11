namespace SarahBeauty.Business.Recovery;

internal sealed class ArchiveWorkspace:IDisposable
{
    internal string Root {get;}
    private readonly HashSet<string> _files=[];
    private readonly HashSet<string> _directories=[];
    internal ArchiveWorkspace(string storeRoot)
    {
        ArchivePaths.RejectLink(storeRoot);string parent=Path.Combine(Path.GetFullPath(storeRoot),"RecoveryStaging");ArchivePaths.RejectLink(parent);Directory.CreateDirectory(parent);
        Root=Path.Combine(parent,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Root);ArchivePaths.RejectLink(Root);
    }
    internal string FilePath(string relative)
    {
        string path=new ArchivePaths(Root).Resolve(relative);string? directory=Path.GetDirectoryName(path);
        while(directory is not null && directory!=Root){_directories.Add(directory);directory=Path.GetDirectoryName(directory);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);_files.Add(path);return path;
    }
    internal string PackagePath(){string path=Path.Combine(Root,"payload.zip");_files.Add(path);return path;}
    public void Dispose()
    {
        foreach(string file in _files)try{File.Delete(file);}catch(IOException){}catch(UnauthorizedAccessException){}
        foreach(string directory in _directories.OrderByDescending(d=>d.Length))DeleteDirectory(directory);
        DeleteDirectory(Root);
    }
    private static void DeleteDirectory(string directory){try{Directory.Delete(directory,recursive:false);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
