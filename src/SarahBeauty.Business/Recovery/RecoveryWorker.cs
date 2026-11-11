using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace SarahBeauty.Business.Recovery;

internal sealed class RecoveryWorker(string executable,TimeSpan timeout)
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};

    internal async Task<RecoveryReply> Run(RecoveryCommand command,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();if(!File.Exists(executable))throw new RecoveryFailure("HelperMissing","Build or reinstall the recovery helper before using portable recovery.");
        if(command.ProtectedIdentity is{Length:>65536} || command.Passphrase?.Length>1024 || command.Recipient?.Length>8192 || command.InputPath?.Length>32768 || command.OutputPath?.Length>32768)
            throw new RecoveryFailure("InvalidRequest","The recovery input exceeds the supported size.");
        byte[] payload=JsonSerializer.SerializeToUtf8Bytes(command,Json);if(payload.Length>128*1024){CryptographicOperations.ZeroMemory(payload);throw new RecoveryFailure("InvalidRequest","The recovery request exceeds the supported size.");}
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(cancellation);limit.CancelAfter(timeout);
        using var process=new Process{StartInfo=new(){FileName=executable,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=new UTF8Encoding(false,true),StandardErrorEncoding=new UTF8Encoding(false,true)}};
        Task<string>? output=null,errors=null;
        try
        {
            if(!process.Start())throw new RecoveryFailure("HelperUnavailable","The recovery helper could not start.");
            output=ReadBounded(process.StandardOutput,128*1024,process,limit.Token);errors=ReadBounded(process.StandardError,4096,process,limit.Token);
            await process.StandardInput.BaseStream.WriteAsync(payload,limit.Token);process.StandardInput.Close();CryptographicOperations.ZeroMemory(payload);
            await Task.WhenAll(process.WaitForExitAsync(limit.Token),output,errors);
            RecoveryReply reply;
            try{reply=JsonSerializer.Deserialize<RecoveryReply>(await output,Json)??throw new JsonException();}
            catch(JsonException){throw new RecoveryFailure("InvalidResponse","The recovery helper returned an invalid response.");}
            if(!reply.Ok || process.ExitCode!=0)throw Failure(reply.Code);
            if(!string.IsNullOrEmpty(reply.Code))throw new RecoveryFailure("InvalidResponse","The recovery helper returned an inconsistent result.");
            return reply;
        }
        catch(Exception error)
        {
            await Stop(process,output,errors);
            if(limit.IsCancellationRequested)
            {
                if(cancellation.IsCancellationRequested)throw new OperationCanceledException("The recovery operation was cancelled.",cancellation);
                throw new TimeoutException("The recovery operation timed out. Its output was not published.");
            }
            if(error is DecoderFallbackException)throw new RecoveryFailure("InvalidResponse","The recovery helper returned invalid text.");
            throw;
        }
        finally{CryptographicOperations.ZeroMemory(payload);}
    }

    private static async Task<string> ReadBounded(StreamReader reader,int maximum,Process process,CancellationToken cancellation)
    {
        var result=new StringBuilder();char[] buffer=new char[1024];int count;
        while((count=await reader.ReadAsync(buffer.AsMemory(),cancellation))>0)
        {
            if(result.Length+count>maximum){Kill(process);throw new RecoveryFailure("InvalidResponse","The recovery helper exceeded its response limit.");}
            result.Append(buffer,0,count);
        }
        return result.ToString();
    }
    private static void Kill(Process process){try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
    private static async Task Stop(Process process,Task<string>? output,Task<string>? errors)
    {
        Kill(process);var pending=new List<Task>();if(output is not null)pending.Add(output);if(errors is not null)pending.Add(errors);
        try{pending.Add(process.WaitForExitAsync());}catch(InvalidOperationException){}
        try{await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));}catch{}
    }
    private static RecoveryFailure Failure(string? code)=>code switch
    {
        "AuthenticationFailed"=>new(code,"The recovery material did not match, or encrypted data changed. Check the selected files and passphrase."),
        "InvalidPassphrase"=>new(code,"Use a word passphrase with 15 to 256 characters."),
        "UnsupportedRecipient"=>new(code,"Use a recovery recipient created by this app."),
        "InputUnavailable"=>new(code,"The recovery source file is unavailable."),
        "OutputUnavailable"=>new(code,"The recovery output could not be created. Check the destination and available space."),
        "SizeLimitExceeded"=>new(code,"The recovery payload exceeds the supported size."),
        "InvalidRequest"=>new(code,"The recovery helper rejected the request."),
        _=>new("OperationFailed","The recovery operation failed. Its output was not published.")
    };
}
