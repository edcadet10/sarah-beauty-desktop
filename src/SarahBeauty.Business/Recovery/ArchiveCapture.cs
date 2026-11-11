namespace SarahBeauty.Business.Recovery;

internal sealed class ArchiveCapture(ArchiveWorkspace workspace,ArchiveManifest manifest):IDisposable
{
    internal ArchiveManifest Manifest {get;}=manifest;
    internal string FilePath(string relative)=>new ArchivePaths(workspace.Root).Resolve(relative);
    public void Dispose()=>workspace.Dispose();
}
