using System.Globalization;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Exports;

internal sealed class ReadableGenerationPaths(string databasePath)
{
    private readonly string _store=Path.GetDirectoryName(Path.GetFullPath(databasePath))!;
    internal string Root=>Path.Combine(_store,"ReadableExports");
    internal string Generation(string id)
    {
        if(!Guid.TryParseExact(id,"N",out var parsed)||parsed==Guid.Empty)throw Invalid();
        CheckRoot();string path=Path.Combine(Root,id);ArchivePaths.RejectLink(path);return path;
    }

    internal string CreateWorkspace()
    {
        CheckRoot();Directory.CreateDirectory(Root);CheckRoot();string path=Path.Combine(Root,"pending-"+Guid.NewGuid().ToString("N"));
        if(Directory.Exists(path)||File.Exists(path))throw Invalid();Directory.CreateDirectory(path);ArchivePaths.RejectLink(path);return path;
    }

    internal string FilePath(string directory,int index)
    {
        CheckRoot();string absolute=Path.GetFullPath(directory);
        if(!string.Equals(Path.GetDirectoryName(absolute),Root,StringComparison.OrdinalIgnoreCase)||index is <-1 or >255)throw Invalid();
        string name=Path.GetFileName(absolute);string id=name.StartsWith("pending-",StringComparison.Ordinal)?name[8..]:name;
        if(!Guid.TryParseExact(id,"N",out var parsed)||parsed==Guid.Empty)throw Invalid();
        ArchivePaths.RejectLink(absolute);string path=Path.Combine(absolute,FileName(index));ArchivePaths.RejectLink(path);return path;
    }

    internal void CheckFiles(string directory,int tables)
    {
        _=FilePath(directory,-1);var expected=Enumerable.Range(-1,tables+1).Select(FileName).ToHashSet(StringComparer.Ordinal);
        foreach(string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            ArchivePaths.RejectLink(entry);
            if(Directory.Exists(entry)||!expected.Remove(Path.GetFileName(entry)))throw Invalid();
        }
        if(expected.Count!=0)throw Invalid();
    }

    private static string FileName(int index)=>index==-1?"manifest.json":"table-"+index.ToString("D3",CultureInfo.InvariantCulture)+".jsonl";
    private void CheckRoot(){ArchivePaths.RejectLink(_store);ArchivePaths.RejectLink(Root);}
    private static InvalidDataException Invalid()=>new("The readable generation path or exact file inventory is unsupported. Existing files are preserved.");
}
