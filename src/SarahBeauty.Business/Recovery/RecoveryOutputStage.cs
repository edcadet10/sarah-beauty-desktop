using System.Security.Cryptography;

namespace SarahBeauty.Business.Recovery;

internal sealed class RecoveryOutputStage:IDisposable,IAsyncDisposable
{
    private readonly string _directory;
    private readonly string _destination;
    internal string Path {get;}
    internal RecoveryOutputStage(string destination)
    {
        _destination=System.IO.Path.GetFullPath(destination);
        if(File.Exists(_destination)||Directory.Exists(_destination))throw new IOException("Choose a new recovery output. An existing destination is preserved.");
        string parent=System.IO.Path.GetDirectoryName(_destination)!;if(!Directory.Exists(parent))throw new DirectoryNotFoundException("The recovery destination folder is unavailable.");
        _directory=System.IO.Path.Combine(parent,".sarah-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
        Path=System.IO.Path.Combine(_directory,"output.partial");
    }
    internal async Task<RecoveryOutput> Publish(RecoveryReply reply,CancellationToken cancellation,Action<Action>? publish=null)
    {
        string hash;
        using(var file=new FileStream(Path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(file.Length!=reply.Bytes || reply.Bytes<0)throw new RecoveryFailure("InvalidResponse","The recovery output size did not match its completed result.");
            hash=Convert.ToHexString(await SHA256.HashDataAsync(file,cancellation));
        }
        if(!string.Equals(hash,reply.Sha256,StringComparison.OrdinalIgnoreCase))throw new RecoveryFailure("InvalidResponse","The recovery output hash did not match its completed result.");
        void Move(){cancellation.ThrowIfCancellationRequested();File.Move(Path,_destination,overwrite:false);}
        if(publish is null)Move();else publish(Move);return new(reply.Bytes,hash);
    }
    public void Dispose()=>DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await DeleteKnownOutput().ConfigureAwait(false);
        // Unexpected directory contents are retained; cleanup never removes them recursively.
        try{Directory.Delete(_directory,recursive:false);}catch(IOException){}catch(UnauthorizedAccessException){}
    }

    private async Task DeleteKnownOutput()
    {
        for(int attempt=0;;attempt++)
        {
            try{File.Delete(Path);return;}
            catch(IOException error) when((error.HResult&0xFFFF) is 32 or 33 && attempt<39)
            {await Task.Delay(25).ConfigureAwait(false);}
            catch(IOException){throw CleanupFailure();}
            catch(UnauthorizedAccessException){throw CleanupFailure();}
        }
    }

    private static RecoveryFailure CleanupFailure()=>new("TemporaryOutputCleanupFailed",
        "The temporary recovery output could not be removed. Close programs using the destination folder and retry.");
}
