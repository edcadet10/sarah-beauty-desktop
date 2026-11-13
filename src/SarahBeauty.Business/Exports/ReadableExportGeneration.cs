using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal sealed record ReadableGenerationSource(int SourceVersion,string GenerationId,string BusinessId,string BusinessName,string StoreId,string InstanceId,string Mode,int SchemaVersion,long Watermark,string CapturedAtUtc);
internal sealed record ReadableGenerationManifest(int Version,ReadableGenerationSource Source,ReadableExportSelection Selection,IReadOnlyList<ReadableExportGroup> Groups,IReadOnlyList<ReadableTableFacts> Tables,long DataRows,long Cells,long RawUtf8Bytes,ReadableJournalFacts Journal);
internal sealed record ReadableExportGeneration(ReadableGenerationManifest Manifest,IReadOnlyList<ReadableExportTable> Tables,string ManifestSha256)
{
    internal const int FormatVersion=3;
    private const int GroupCount=31,BusinessTableCount=161,TableCount=165;
    internal static ReadableExportGeneration Build(ReadableExportSnapshot snapshot,ReadableExportSelection selection,CancellationToken cancellation=default)
    {
        var assembly=ReadableExportAssembly.Build(snapshot,selection,cancellation);
        var metadata=ReadableExportMetadata.Build(assembly,new(cancellation:cancellation));
        var dictionary=ReadableColumnDictionary.Build(assembly,metadata,new(cancellation:cancellation));
        var tables=ReadableTableVerification.CopyAll(snapshot.Source,assembly.Tables.Concat(metadata).Append(dictionary),new(cancellation:cancellation));
        var source=snapshot.Source;var identity=new ReadableGenerationSource(source.Version,source.GenerationId,source.BusinessId,source.BusinessName,source.StoreId,source.InstanceId,source.Mode,source.SchemaVersion,source.Watermark,source.CapturedAtUtc);
        var manifest=Describe(identity,selection,assembly.Groups,tables);CheckSource(manifest);cancellation.ThrowIfCancellationRequested();return new(manifest,tables,Hash(manifest));
    }

    internal static ReadableExportGeneration Verify(ReadableGenerationManifest manifest,string expectedSha256,IEnumerable<ReadableExportTable> tables,CancellationToken cancellation=default)
    {
        CheckManifest(manifest,expectedSha256);
        var source=manifest.Source;var copies=ReadableTableVerification.CopyAll(source.GenerationId,source.Watermark,tables,new(cancellation:cancellation));
        var rebuilt=Describe(source,manifest.Selection,manifest.Groups,copies);
        if(Hash(rebuilt)!=expectedSha256)throw Invalid();cancellation.ThrowIfCancellationRequested();return new(rebuilt,copies,expectedSha256);
    }

    internal static void CheckManifest(ReadableGenerationManifest manifest,string expectedSha256)
    {
        CheckSource(manifest);manifest.Selection.Validate();ReadableGenerationFacts.CheckJournal(manifest.Journal);
        if(manifest.Groups.Any(g=>g is null||string.IsNullOrWhiteSpace(g.Name)||g.Name.Length>64||g.Tables is null||g.Tables.Count is <1 or >BusinessTableCount)||
            manifest.Tables.Any(t=>t is null||string.IsNullOrWhiteSpace(t.Name)||t.Name.Length>64||t.Columns is <1 or >128||t.DataRows is <0 or >1_000_000||
                t.Cells!=(t.DataRows+1L)*t.Columns||t.RawUtf8Bytes is <0 or >64*1024*1024||t.Sha256 is not{Length:64}||!t.Sha256.All(char.IsAsciiHexDigit)))throw Invalid();
        CheckInventory(manifest.Groups,manifest.Tables.Select(t=>t.Name).ToArray());
        if(manifest.DataRows!=manifest.Tables.Sum(t=>(long)t.DataRows)||manifest.Cells!=manifest.Tables.Sum(t=>t.Cells)||manifest.Cells>1_000_000||
            manifest.RawUtf8Bytes!=manifest.Tables.Sum(t=>t.RawUtf8Bytes)||manifest.RawUtf8Bytes>64*1024*1024||manifest.Journal.Rows!=manifest.Tables.Single(t=>t.Name=="Journal lines").DataRows||Hash(manifest)!=expectedSha256)throw Invalid();
    }

    private static ReadableGenerationManifest Describe(ReadableGenerationSource source,ReadableExportSelection selection,IReadOnlyList<ReadableExportGroup> groups,IReadOnlyList<ReadableExportTable> tables)
    {
        CheckInventory(groups,tables.Select(t=>t.Name).ToArray());var facts=Array.AsReadOnly(tables.Select(ReadableGenerationFacts.Table).ToArray());
        var copiedGroups=Array.AsReadOnly(groups.Select(g=>new ReadableExportGroup(g.Name,Array.AsReadOnly(g.Tables.ToArray()))).ToArray());
        return new(FormatVersion,source,selection,copiedGroups,facts,facts.Sum(t=>(long)t.DataRows),facts.Sum(t=>t.Cells),facts.Sum(t=>t.RawUtf8Bytes),ReadableGenerationFacts.Journal(tables.Single(t=>t.Name=="Journal lines")));
    }

    private static void CheckInventory(IReadOnlyList<ReadableExportGroup> groups,IReadOnlyList<string> tables)
    {
        string[] metadata=["Start Here","Export conventions","Business table inventory","Column dictionary"];
        if(groups.Count!=GroupCount||tables.Count!=TableCount||tables.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=TableCount||groups.Any(g=>string.IsNullOrWhiteSpace(g.Name)||g.Tables.Count==0)||groups.Select(g=>g.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=GroupCount)throw Invalid();
        string[] members=groups.SelectMany(g=>g.Tables).ToArray();
        if(members.Length!=BusinessTableCount||members.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=BusinessTableCount||members.Intersect(metadata,StringComparer.OrdinalIgnoreCase).Any()||!tables.ToHashSet(StringComparer.Ordinal).SetEquals(members.Concat(metadata)))throw Invalid();
    }

    private static void CheckSource(ReadableGenerationManifest manifest)
    {
        if(manifest is null||manifest.Source is null||manifest.Selection is null||manifest.Tables is null||manifest.Groups is null||manifest.Journal is null)throw Invalid();var source=manifest.Source;
        if(manifest.Version!=FormatVersion||source.SourceVersion!=1||source.SchemaVersion!=BusinessSchema.Version||source.Mode is not("TEST" or "LIVE")||source.Watermark<0||string.IsNullOrWhiteSpace(source.BusinessName)||
            !Guid.TryParseExact(source.GenerationId,"N",out _)||!Guid.TryParse(source.BusinessId,out _)||!Guid.TryParse(source.StoreId,out _)||!Guid.TryParse(source.InstanceId,out _)||
            !DateTimeOffset.TryParseExact(source.CapturedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)||time.Offset!=TimeSpan.Zero||manifest.Tables.Count!=TableCount)throw Invalid();
    }

    private static string Hash(ReadableGenerationManifest manifest)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(manifest)));
    private static InvalidDataException Invalid()=>new("The readable generation does not match its supported source, complete table inventory, or trusted manifest digest.");
}
