namespace SarahBeauty.Business.Recovery;

public sealed class StagedArchiveReview:IDisposable
{
    private readonly ArchiveWorkspace _workspace;
    public ArchiveManifest Manifest {get;}
    public string ManifestHash {get;}
    public string ArchiveHash {get;}
    public long ArchiveBytes {get;}
    public bool MatchedRecordedArchive {get;}
    internal StagedArchiveReview(ArchiveWorkspace workspace,ExtractedArchive extracted,string hash,long bytes,bool matched)
        =>(_workspace,Manifest,ManifestHash,ArchiveHash,ArchiveBytes,MatchedRecordedArchive)=(workspace,extracted.Manifest,extracted.ManifestHash,hash,bytes,matched);
    internal string FilePath(string relative)=>new ArchivePaths(_workspace.Root).Resolve(relative);
    public void Dispose()=>_workspace.Dispose();
}
