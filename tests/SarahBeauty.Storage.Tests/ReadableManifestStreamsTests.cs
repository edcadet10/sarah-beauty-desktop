using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableManifestStreamsTests
{
    [Fact]
    public void BoundedManifestRoundTripsAndRejectsAmbiguousUnknownOrTruncatedJson()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var dates=new ReadableExportSelection(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
        var generation=ReadableExportGeneration.Build(source,dates);using var stream=new MemoryStream();
        ReadableManifestStreams.Write(stream,generation.Manifest,generation.ManifestSha256);stream.Position=0;
        var actual=ReadableManifestStreams.Read(stream,generation.ManifestSha256);
        Assert.Equal(JsonSerializer.Serialize(generation.Manifest),JsonSerializer.Serialize(actual));
        string json=Encoding.UTF8.GetString(stream.ToArray());
        foreach(string invalid in new[]{"{\"Version\":1,"+json[1..],"{\"FutureSecret\":\"Fictional private\","+json[1..],json[..^1],json.Replace("\"Groups\":[","\"Groups\":null,\"DiscardedGroups\":[")})
        {
            using var bad=new MemoryStream(Encoding.UTF8.GetBytes(invalid));
            Assert.Throws<InvalidDataException>(()=>ReadableManifestStreams.Read(bad,generation.ManifestSha256));
        }
        stream.Position=0;Assert.Throws<InvalidDataException>(()=>ReadableManifestStreams.Read(stream,new string('0',64)));
        stream.Position=0;Assert.Throws<InvalidDataException>(()=>ReadableManifestStreams.Write(stream,generation.Manifest,generation.ManifestSha256));
        using var oversized=new MemoryStream(new byte[ReadableManifestStreams.MaximumBytes+1]);
        Assert.Throws<InvalidDataException>(()=>ReadableManifestStreams.Read(oversized,generation.ManifestSha256));
        Reject(generation.Manifest with{Groups=null!});Reject(generation.Manifest with{Source=null!});
        Reject(generation.Manifest with{Journal=generation.Manifest.Journal with{DebitCents="1"}});
        Reject(generation.Manifest with{Journal=generation.Manifest.Journal with{DebitCents=null!}});
        var descriptors=generation.Manifest.Tables.ToArray();descriptors[0]=descriptors[0] with{Columns=129};
        Reject(generation.Manifest with{Tables=descriptors});
        void Reject(ReadableGenerationManifest invalid)
        {
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(invalid);string hash=Convert.ToHexString(SHA256.HashData(bytes));
            using var input=new MemoryStream(bytes);Assert.Throws<InvalidDataException>(()=>ReadableManifestStreams.Read(input,hash));
        }
    }
}
