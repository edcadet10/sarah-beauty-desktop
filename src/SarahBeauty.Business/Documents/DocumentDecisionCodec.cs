using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SarahBeauty.Business.Documents;

internal static class DocumentDecisionCodec
{
    internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static DocumentDecision Read(string json,string expectedHash)
    {
        try
        {
            if(Hash(json)!=expectedHash)throw Invalid();var value=JsonSerializer.Deserialize<DocumentDecision>(json)??throw Invalid();
            if(value.Version!=1 || new[]{value.Id,value.BusinessId,value.DocumentId,value.VersionId,value.RequestId,value.OwnerId}.Any(id=>DocumentDecisionRules.Identity(id)!=id))throw Invalid();
            var draft=Draft(value);if(DocumentDecisionRules.Normalize(draft)!=draft || DocumentDecisionRules.Result(value.Action)!=value.ResultStatus)throw Invalid();
            if(!DateTimeOffset.TryParseExact(value.RecordedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)||at.Offset!=TimeSpan.Zero||JsonSerializer.Serialize(value)!=json)throw Invalid();
            return value;
        }
        catch(Exception error) when(error is JsonException or ArgumentException){throw new InvalidDataException("The retained document decision is invalid.",error);}
    }
    internal static DocumentDecisionDraft Draft(DocumentDecision value)=>new(value.VersionId,value.DocumentSnapshotHash,value.PriorStatus,value.Action,value.DecisionDate,value.Reason,value.Evidence);
    internal static string Payload(DocumentDecisionDraft draft)=>Hash(JsonSerializer.Serialize(draft));
    internal static InvalidDataException Invalid()=>new("The retained document decision does not match its source, identity, or supported transition.");
}
