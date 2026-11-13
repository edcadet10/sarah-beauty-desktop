using System.Text.Json;

namespace SarahBeauty.Business.Security;

internal sealed record OwnerPinMaterial(int Version,string OwnerId,string CredentialStamp,string Salt,string Hash,int Iterations)
{
    private const string Purpose="SarahBeauty owner PIN v1\n";
    internal static bool ValidPin(string pin)=>pin is {Length:>=6 and <=12}&&pin.All(c=>c is >= '0' and <= '9');
    internal static OwnerPinMaterial Create(OwnerSession owner,string pin)
    {
        var value=Passphrase.Hash(Purpose+pin);
        return new(1,owner.OwnerId,owner.CredentialStamp!,value.Salt,value.Hash,Passphrase.Iterations);
    }
    internal bool Verify(string ownerId,string stamp,string pin)
    {
        if(Version!=1||OwnerId!=ownerId||CredentialStamp!=stamp||!ValidPin(pin)||Iterations!=Passphrase.Iterations)return false;
        try{return Convert.FromBase64String(Salt).Length==32&&Convert.FromBase64String(Hash).Length==32&&Passphrase.Verify(Purpose+pin,Salt,Hash,Iterations);}
        catch(Exception error) when(error is FormatException or ArgumentException){return false;}
    }
    internal static OwnerPinMaterial? Read(byte[]? clear)
    {
        try{return clear is null?null:JsonSerializer.Deserialize<OwnerPinMaterial>(clear);}
        catch(JsonException){return null;}
    }
}
