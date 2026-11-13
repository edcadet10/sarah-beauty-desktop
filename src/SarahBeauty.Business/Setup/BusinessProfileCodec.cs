using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SarahBeauty.Business.Setup;

internal static class BusinessProfileCodec
{
    internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static BusinessProfileChange Change(BusinessProfileVersion value)=>new(value.Revision-1,value.Profile,value.ChangeReason);
    internal static string Payload(BusinessProfileChange value)=>Hash(JsonSerializer.Serialize(value));
    internal static BusinessProfileVersion Read(string json,string expectedHash)
    {
        try
        {
            if(Hash(json)!=expectedHash)throw Invalid();var value=JsonSerializer.Deserialize<BusinessProfileVersion>(json)??throw Invalid();
            if(value.Version!=1||value.Revision<1||value.Mode is not("TEST" or "LIVE")||new[]{value.Id,value.BusinessId,value.RequestId,value.RecordedByOwnerId}.Any(id=>!Guid.TryParseExact(id,"D",out var guid)||guid==Guid.Empty||guid.ToString()!=id))throw Invalid();
            var change=Change(value);if(JsonSerializer.Serialize(BusinessProfileRules.NormalizeChange(change))!=JsonSerializer.Serialize(change)||JsonSerializer.Serialize(value)!=json)throw Invalid();
            if(!DateTimeOffset.TryParseExact(value.RecordedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)||at.Offset!=TimeSpan.Zero)throw Invalid();return value;
        }
        catch(Exception error) when(error is JsonException or ArgumentException){throw new InvalidDataException("The private business profile is invalid.",error);}
    }
    internal static InvalidDataException Invalid()=>new("The private business-profile history does not match its identity, revision, recording owner or supporting evidence.");
}
