using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal sealed record ReadableGenerationArtifact(string GenerationId,string ManifestSha256);
internal sealed class ReadableGenerationFiles(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    internal ReadableGenerationArtifact Stage(ReadableExportGeneration generation,CancellationToken cancellation=default)
    {
        RequireCurrent(generation.Manifest.Source);var verified=ReadableExportGeneration.Verify(generation.Manifest,generation.ManifestSha256,generation.Tables,cancellation);
        var paths=new ReadableGenerationPaths(store.DatabasePath);using var workspace=new ReadableGenerationWorkspace(paths);
        for(int i=0;i<verified.Tables.Count;i++)
        {
            cancellation.ThrowIfCancellationRequested();using var stream=workspace.Create(i);ReadableTableStreams.Write(stream,verified.Tables[i],cancellation);stream.Flush(flushToDisk:true);
        }
        using(var stream=workspace.Create(-1)){ReadableManifestStreams.Write(stream,verified.Manifest,verified.ManifestSha256);stream.Flush(flushToDisk:true);}
        FailurePoint?.Invoke("Files written");var reread=ReadDirectory(paths,workspace.Root,verified.ManifestSha256,cancellation);
        FailurePoint?.Invoke("Before publish");
        store.Transaction(db=>{RequireCurrent(db,reread.Manifest.Source);paths.CheckFiles(workspace.Root,reread.Tables.Count);cancellation.ThrowIfCancellationRequested();workspace.Publish(reread.Manifest.Source.GenerationId);return true;});
        return new(reread.Manifest.Source.GenerationId,reread.ManifestSha256);
    }

    internal ReadableExportGeneration Load(ReadableGenerationArtifact artifact,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();store.Read(_=>true);var paths=new ReadableGenerationPaths(store.DatabasePath);
        var generation=ReadDirectory(paths,paths.Generation(artifact.GenerationId),artifact.ManifestSha256,cancellation);
        if(generation.Manifest.Source.GenerationId!=artifact.GenerationId)throw new InvalidDataException("The staged readable generation does not match its job identity.");
        RequireCurrent(generation.Manifest.Source);return generation;
    }

    private static ReadableExportGeneration ReadDirectory(ReadableGenerationPaths paths,string directory,string digest,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();ReadableGenerationManifest manifest;
        using(var stream=Open(paths.FilePath(directory,-1)))manifest=ReadableManifestStreams.Read(stream,digest);
        paths.CheckFiles(directory,manifest.Tables.Count);var budget=new ReadableExportBudget(cancellation:cancellation);var tables=new List<ReadableExportTable>();
        for(int i=0;i<manifest.Tables.Count;i++)
        {
            cancellation.ThrowIfCancellationRequested();using var stream=Open(paths.FilePath(directory,i));
            tables.Add(ReadableTableStreams.Read(stream,manifest.Source,manifest.Tables[i],budget,cancellation));
        }
        paths.CheckFiles(directory,manifest.Tables.Count);return ReadableExportGeneration.Verify(manifest,digest,tables,cancellation);
    }

    private static FileStream Open(string path)=>new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
    private void RequireCurrent(ReadableGenerationSource source)=>store.Read(db=>{RequireCurrent(db,source);return true;});
    private void RequireCurrent(DbWork db,ReadableGenerationSource source)
    {
        StoreInstanceGuard.Require(db,store.StoreId,store.InstanceId);
        if(source.StoreId!=store.StoreId||source.InstanceId!=store.InstanceId||source.Mode!=(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")||
            db.Scalar("SELECT Id FROM Business WHERE Id=$id AND Watermark>=$watermark;",("$id",source.BusinessId),("$watermark",source.Watermark)) is null)
            throw new UnauthorizedAccessException("The readable generation belongs to another or unavailable business-store instance.");
    }
}
