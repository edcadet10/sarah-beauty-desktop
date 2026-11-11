namespace SarahBeauty.Business.Recovery;

public sealed class RecoveryCipher
{
    private readonly RecoveryWorker _worker;
    public RecoveryCipher():this(Path.Combine(AppContext.BaseDirectory,"SarahBeauty.Recovery.exe"),TimeSpan.FromMinutes(30)){}
    internal RecoveryCipher(string executable,TimeSpan timeout)=>_worker=new(executable,timeout);

    public async Task<RecoveryMaterial> CreateAsync(string passphrase,CancellationToken cancellation=default)
    {
        var result=await _worker.Run(new(){Operation="create-recovery",Passphrase=passphrase},cancellation);
        if(string.IsNullOrWhiteSpace(result.Recipient)||result.Recipient.Length>8192||result.ProtectedIdentity is not{Length:>0 and <=65536})throw new RecoveryFailure("InvalidResponse","The protected recovery material was incomplete.");
        return new(result.Recipient,result.ProtectedIdentity);
    }
    public async Task<string> InspectAsync(byte[] protectedIdentity,string passphrase,CancellationToken cancellation=default)
    {
        var result=await _worker.Run(new(){Operation="inspect-recovery",ProtectedIdentity=protectedIdentity,Passphrase=passphrase},cancellation);
        return !string.IsNullOrWhiteSpace(result.Recipient)&&result.Recipient.Length<=8192?result.Recipient:throw new RecoveryFailure("InvalidResponse","The recovery recipient was missing.");
    }
    public async Task<RecoveryOutput> EncryptAsync(string input,string output,string recipient,CancellationToken cancellation=default)
    {
        using var stage=new RecoveryOutputStage(output);
        var result=await _worker.Run(new(){Operation="encrypt",InputPath=Path.GetFullPath(input),OutputPath=stage.Path,Recipient=recipient},cancellation);
        return await stage.Publish(result,cancellation);
    }
    public async Task<RecoveryOutput> DecryptAsync(string input,string output,byte[] protectedIdentity,string passphrase,CancellationToken cancellation=default)
    {
        using var stage=new RecoveryOutputStage(output);
        var result=await _worker.Run(new(){Operation="decrypt",InputPath=Path.GetFullPath(input),OutputPath=stage.Path,ProtectedIdentity=protectedIdentity,Passphrase=passphrase},cancellation);
        return await stage.Publish(result,cancellation);
    }
}
