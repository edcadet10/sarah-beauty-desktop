using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Setup;

internal sealed class LiveBusinessPaths
{
    internal string Root {get;}
    internal string Primary=>Path.Combine(Root,"Primary");
    internal string Database=>Path.Combine(Primary,"sarahbeauty.db");
    internal string Selection=>Path.Combine(Root,"active-live-store.json");
    internal string SelectionLock=>Path.Combine(Root,".active-live-store.lock");
    internal LiveBusinessPaths(string root){ArgumentException.ThrowIfNullOrWhiteSpace(root);Root=Path.GetFullPath(root);}
    internal void RequireNoLinks()
    {
        for(DirectoryInfo? path=new(Root);path is not null;path=path.Parent)ArchivePaths.RejectLink(path.FullName);
        ArchivePaths.RejectLink(Primary);ArchivePaths.RejectLink(Database);ArchivePaths.RejectLink(Selection);ArchivePaths.RejectLink(SelectionLock);
    }
    internal void RequireUnused()
    {
        RequireNoLinks();if(File.Exists(Primary)||Directory.Exists(Primary)||File.Exists(Selection)||Directory.Exists(Selection))throw new IOException("A LIVE business destination or selection already exists. Preserve it and open or review that business.");
    }
}
