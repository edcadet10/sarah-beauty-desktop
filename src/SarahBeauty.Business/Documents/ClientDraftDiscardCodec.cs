using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SarahBeauty.Business.Documents;

internal static class ClientDraftDiscardCodec
{
    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static ClientDraftDiscardRequest Request(ClientDraftDiscard value)=>new(value.Kind,value.DraftId,value.DraftRevision,value.DraftSnapshotHash,value.Reason);
    internal static string Payload(ClientDraftDiscardRequest value)=>Hash(JsonSerializer.Serialize(value));
    internal static ClientDraftDiscard Read(string json,string expectedHash)
    {
        try
        {
            if(Hash(json)!=expectedHash)throw Invalid();var value=JsonSerializer.Deserialize<ClientDraftDiscard>(json)??throw Invalid();
            if(value.Version!=1||value.Mode is not("TEST" or "LIVE")||new[]{value.Id,value.BusinessId,value.RequestId,value.OwnerId}.Any(id=>!Guid.TryParseExact(id,"D",out var guid)||guid==Guid.Empty||guid.ToString()!=id)||JsonSerializer.Serialize(value)!=json)throw Invalid();
            var request=Request(value);if(JsonSerializer.Serialize(ClientDraftDiscardRules.Normalize(request))!=JsonSerializer.Serialize(request)||Hash(value.DraftSnapshotJson)!=value.DraftSnapshotHash)throw Invalid();
            var source=ClientDraftSnapshots.Read(value.Kind,value.DraftSnapshotJson);if(source.Id!=value.DraftId||source.Revision!=value.DraftRevision||ClientDraftSnapshots.Status(source)!="Draft"||ClientDraftSnapshots.IssuedVersion(source)!="")throw Invalid();
            if(!DateTimeOffset.TryParseExact(value.RecordedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)||at.Offset!=TimeSpan.Zero)throw Invalid();return value;
        }
        catch(Exception error) when(error is JsonException or ArgumentException){throw new InvalidDataException("The private client draft discard history is invalid.",error);}
    }
    internal static InvalidDataException Invalid()=>new("The retained client draft discard does not match its source, identity, recording owner or current draft state.");
}
