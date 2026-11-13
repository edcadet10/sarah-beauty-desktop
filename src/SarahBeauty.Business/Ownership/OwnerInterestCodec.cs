using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SarahBeauty.Business.Ownership;

internal static class OwnerInterestCodec
{
    internal static string Hash(string json)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    internal static OwnerInterestVersion Read(string json,string hash)
    {
        try
        {
            if(Hash(json)!=hash)throw Invalid();
            var value=JsonSerializer.Deserialize<OwnerInterestVersion>(json)??throw Invalid();Validate(value);
            if(JsonSerializer.Serialize(value)!=json)throw Invalid();
            return value with{Shares=Array.AsReadOnly(value.Shares.ToArray())};
        }
        catch(Exception error) when(error is JsonException or ArgumentException or OverflowException){throw new InvalidDataException("The retained ownership confirmation is invalid.",error);}
    }
    private static void Validate(OwnerInterestVersion value)
    {
        if(value.FormatVersion!=1 || value.Revision<1 || !Id(value.Id) || !Id(value.BusinessId) || !Id(value.RecordedByOwnerId) || value.Shares is null || value.Shares.Any(s=>s is null || string.IsNullOrWhiteSpace(s.OwnerName)))throw Invalid();
        var inputs=value.Shares.Select(s=>new OwnerInterestInput(s.OwnerId,s.Fraction)).ToArray();
        var normalized=OwnerInterestRules.Normalize(new(value.Revision-1,value.EffectiveFrom,inputs,value.Reason,value.SupersedesId,value.EvidenceVersionId));
        if(normalized.Reason!=value.Reason || normalized.SupersedesId!=value.SupersedesId || normalized.EvidenceVersionId!=value.EvidenceVersionId || !inputs.SequenceEqual(normalized.Shares))throw Invalid();
        if((value.EvidenceVersionId is null)!=(value.EvidenceSnapshotHash is null) || value.EvidenceSnapshotHash is string hash && (hash.Length!=64 || hash.Any(c=>!"0123456789ABCDEF".Contains(c))))throw Invalid();
        if(!DateTimeOffset.TryParseExact(value.RecordedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var at) || at.Offset!=TimeSpan.Zero)throw Invalid();
    }
    private static bool Id(string? value)=>Guid.TryParseExact(value,"D",out var id)&&id.ToString()==value;
    internal static InvalidDataException Invalid()=>new("The retained ownership confirmation does not match its stored identity, shares, or history.");
}
