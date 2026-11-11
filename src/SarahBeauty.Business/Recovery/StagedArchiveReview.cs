using System.IO.Compression;

namespace SarahBeauty.Business.Recovery;

public sealed class StagedArchiveReview:IDisposable
{
    private readonly ArchiveWorkspace _workspace;
    private readonly object _sync=new();
    private bool _disposed;
    public ArchiveManifest Manifest {get;}
    public string ManifestHash {get;}
    public string ArchiveHash {get;}
    public long ArchiveBytes {get;}
    public bool MatchedRecordedArchive {get;}
    internal StagedArchiveReview(ArchiveWorkspace workspace,ExtractedArchive extracted,string hash,long bytes,bool matched)
        =>(_workspace,Manifest,ManifestHash,ArchiveHash,ArchiveBytes,MatchedRecordedArchive)=(workspace,extracted.Manifest,extracted.ManifestHash,hash,bytes,matched);
    internal string FilePath(string relative){lock(_sync){ObjectDisposedException.ThrowIf(_disposed,this);return new ArchivePaths(_workspace.Root).Resolve(relative);}}
    internal ArchiveManifest VerifiedManifest(CancellationToken cancellation)
    {
        lock(_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);using var zip=ZipFile.OpenRead(_workspace.PackagePath());var raw=ArchiveExtraction.ReadManifest(zip,cancellation);
            if(raw.ManifestHash!=ManifestHash)throw new InvalidDataException("The staged recovery manifest changed after validation.");return raw.Manifest;
        }
    }
    internal void PublishPreparedCopy(Action publish){lock(_sync){ObjectDisposedException.ThrowIf(_disposed,this);publish();}}
    public void Dispose(){lock(_sync){if(_disposed)return;_disposed=true;_workspace.Dispose();}}
}
