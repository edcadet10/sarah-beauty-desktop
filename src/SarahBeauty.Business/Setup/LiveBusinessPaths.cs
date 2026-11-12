using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Setup;

internal sealed class LiveBusinessPaths
{
    internal string Root {get;}
    internal string Primary=>Path.Combine(Root,"Primary");
    internal string Database=>Path.Combine(Primary,"sarahbeauty.db");
    internal LiveBusinessPaths(string root){ArgumentException.ThrowIfNullOrWhiteSpace(root);Root=Path.GetFullPath(root);}
    internal void RequireNoLinks()
    {
        for(DirectoryInfo? path=new(Root);path is not null;path=path.Parent)ArchivePaths.RejectLink(path.FullName);
        ArchivePaths.RejectLink(Primary);ArchivePaths.RejectLink(Database);
    }
    internal void RequireUnused()
    {
        RequireNoLinks();if(File.Exists(Primary)||Directory.Exists(Primary))throw new IOException("A LIVE business destination already exists. Preserve it and open or review that business.");
    }
}
