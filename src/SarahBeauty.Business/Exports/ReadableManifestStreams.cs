using System.Text.Json;
using System.Text.Json.Serialization;

namespace SarahBeauty.Business.Exports;

internal static class ReadableManifestStreams
{
    internal const int MaximumBytes=1024*1024;
    private static readonly JsonSerializerOptions Options=new(){MaxDepth=16,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};

    internal static void Write(Stream destination,ReadableGenerationManifest manifest,string expectedSha256)
    {
        ReadableExportGeneration.CheckManifest(manifest,expectedSha256);
        if(!destination.CanWrite||!destination.CanSeek||destination.Length!=0||destination.Position!=0)throw Invalid();
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(manifest,Options);if(bytes.Length>MaximumBytes)throw Invalid();destination.Write(bytes);
    }

    internal static ReadableGenerationManifest Read(Stream source,string expectedSha256)
    {
        if(!source.CanRead||!source.CanSeek||source.Position!=0||source.Length is <1 or >MaximumBytes)throw Invalid();
        byte[] bytes=new byte[checked((int)source.Length)];source.ReadExactly(bytes);
        try
        {
            RejectDuplicateProperties(bytes);
            var manifest=JsonSerializer.Deserialize<ReadableGenerationManifest>(bytes,Options)??throw Invalid();
            ReadableExportGeneration.CheckManifest(manifest,expectedSha256);return manifest;
        }
        catch(JsonException){throw Invalid();}
    }

    private static void RejectDuplicateProperties(byte[] bytes)
    {
        var reader=new Utf8JsonReader(bytes,new JsonReaderOptions{MaxDepth=16});var objects=new Stack<HashSet<string>>();
        while(reader.Read())
        {
            if(reader.TokenType==JsonTokenType.StartObject)objects.Push(new(StringComparer.Ordinal));
            else if(reader.TokenType==JsonTokenType.EndObject)objects.Pop();
            else if(reader.TokenType==JsonTokenType.PropertyName&&!objects.Peek().Add(reader.GetString()!))throw Invalid();
        }
    }

    private static InvalidDataException Invalid()=>new("The staged readable manifest has unsupported content, size, or trusted digest.");
}
