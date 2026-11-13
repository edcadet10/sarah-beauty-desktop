using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Setup;

internal sealed class LiveCreationStage
{
    private readonly string _root;
    private readonly string _pending;
    internal string Destination {get;}
    internal string Database=>Path.Combine(_pending,"sarahbeauty.db");
    internal string PublishedDatabase=>Path.Combine(Destination,"sarahbeauty.db");

    internal LiveCreationStage(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);_root=Path.GetFullPath(root);Destination=Path.Combine(_root,"Primary");
        RequireUnused();Directory.CreateDirectory(_root);RequireUnused();_pending=Path.Combine(_root,".pending-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_pending);
    }
    internal void Publish(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();RequireUnused();ArchivePaths.RejectLink(_pending);Directory.Move(_pending,Destination);
    }
    private void RequireUnused()
    {
        for(DirectoryInfo? path=new(_root);path is not null;path=path.Parent)ArchivePaths.RejectLink(path.FullName);
        ArchivePaths.RejectLink(Destination);
        if(File.Exists(Destination)||Directory.Exists(Destination))throw new IOException("A LIVE business destination already exists. Preserve it and open or review that business.");
    }
}
