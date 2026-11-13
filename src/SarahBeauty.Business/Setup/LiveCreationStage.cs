using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Setup;

internal sealed class LiveCreationStage
{
    private readonly LiveBusinessPaths _paths;
    private readonly string _pending;
    internal string Database=>Path.Combine(_pending,"sarahbeauty.db");
    internal string PublishedDatabase=>_paths.Database;

    internal LiveCreationStage(string root)
    {
        _paths=new(root);_paths.RequireUnused();Directory.CreateDirectory(_paths.Root);_paths.RequireUnused();_pending=Path.Combine(_paths.Root,".pending-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_pending);
    }
    internal void Publish(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();_paths.RequireNoLinks();using var selectionLock=new FileStream(_paths.SelectionLock,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        _paths.RequireUnused();ArchivePaths.RejectLink(_pending);cancellation.ThrowIfCancellationRequested();Directory.Move(_pending,_paths.Primary);
    }
}
