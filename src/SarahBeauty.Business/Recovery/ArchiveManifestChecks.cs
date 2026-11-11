using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal static class ArchiveManifestChecks
{
    internal const long MaximumBytes=64L*1024*1024*1024;
    internal static ArchiveManifest Read(byte[] bytes)
    {
        ArchiveManifest manifest;
        try{manifest=JsonSerializer.Deserialize<ArchiveManifest>(bytes,new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,MaxDepth=32})??throw new InvalidDataException("The recovery manifest is empty.");}
        catch(JsonException error){throw new InvalidDataException("The recovery manifest is not supported.",error);}
        if(manifest.Version!=1 || manifest.SchemaVersion!=BusinessSchema.Version || manifest.Mode is not("TEST" or "LIVE"))throw new InvalidDataException("The recovery format, schema, or mode is not supported by this app.");
        if(!Guid.TryParse(manifest.BusinessId,out _) || !Guid.TryParse(manifest.StoreId,out _) || string.IsNullOrWhiteSpace(manifest.BusinessName) || manifest.Watermark<0 || !DateTimeOffset.TryParse(manifest.CreatedAtUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _) || string.IsNullOrWhiteSpace(manifest.AppVersion))throw new InvalidDataException("The recovery manifest identity or capture facts are incomplete.");
        if(manifest.Files is not{Length:>0 and <=100000} || manifest.Facts is null || manifest.Facts.Tables is null)throw new InvalidDataException("The recovery manifest has no supported file and database facts.");
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var file in manifest.Files)
        {
            if(file is null || file.RelativePath is null || ArchivePaths.Normalize(file.RelativePath)!=file.RelativePath || !paths.Add(file.RelativePath) || file.ByteLength<0 || file.ByteLength>MaximumBytes || !Hash(file.Sha256))throw new InvalidDataException("A recovery file entry is invalid or duplicated.");
            string kind=file.RelativePath=="sarahbeauty.db"?"Database":file.RelativePath.StartsWith("Attachments/",StringComparison.Ordinal)?"Attachment":"PDF";
            if(file.Kind!=kind)throw new InvalidDataException("A recovery file kind does not match its managed path.");total=checked(total+file.ByteLength);if(total>MaximumBytes)throw new InvalidDataException("The recovery store exceeds the supported size.");
        }
        if(!paths.Contains("sarahbeauty.db"))throw new InvalidDataException("The recovery database is missing from the manifest.");return manifest;
    }
    internal static bool Hash(string? value)=>value is{Length:64} && value.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
