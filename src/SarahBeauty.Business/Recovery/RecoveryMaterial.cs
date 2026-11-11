namespace SarahBeauty.Business.Recovery;

public sealed record RecoveryMaterial(string Recipient,byte[] ProtectedIdentity);
public sealed record RecoveryOutput(long ByteLength,string Sha256);

internal sealed class RecoveryCommand
{
    public int Version {get;init;}=1;
    public required string Operation {get;init;}
    public string? Passphrase {get;init;}
    public byte[]? ProtectedIdentity {get;init;}
    public string? Recipient {get;init;}
    public string? InputPath {get;init;}
    public string? OutputPath {get;init;}
    public override string ToString()=>"Recovery command: "+Operation;
}
internal sealed class RecoveryReply
{
    public bool Ok {get;init;}
    public string? Code {get;init;}
    public string? Recipient {get;init;}
    public byte[]? ProtectedIdentity {get;init;}
    public string? Sha256 {get;init;}
    public long Bytes {get;init;}
}

public sealed class RecoveryFailure(string code,string message):IOException(message)
{
    public string Code {get;}=code;
}
