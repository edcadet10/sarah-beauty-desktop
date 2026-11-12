using System.Text.Json;
using System.Security.Cryptography;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleSheetsValueBatch(string GenerationId,long Watermark,IReadOnlyList<SheetsWriteRectangle> Rectangles,ReadOnlyMemory<byte> Json);
internal static class GoogleSheetsValueRequests
{
    internal const int MaximumRequestBytes=1_500_000;
    private const int MaximumRanges=100,RangeEnvelopeReserve=512;

    internal static byte[] CopyPayload(GoogleSheetsValueBatch batch,CancellationToken cancellation)
    {
        if(!Guid.TryParseExact(batch.GenerationId,"N",out var id)||id==Guid.Empty||batch.Watermark<0||batch.Rectangles.Count is <1 or >MaximumRanges||batch.Json.Length>MaximumRequestBytes)throw Limit();
        var budget=new ReadableExportBudget(cancellation:cancellation);
        var rectangles=batch.Rectangles.Select(r=>GoogleSheetsRequestShapes.Copy(r,budget)).ToArray();byte[] bytes=Serialize(rectangles);
        if(bytes.Length>MaximumRequestBytes||!bytes.AsSpan().SequenceEqual(batch.Json.Span)){CryptographicOperations.ZeroMemory(bytes);throw Limit();}return bytes;
    }

    internal static IEnumerable<GoogleSheetsValueBatch> Build(ReadableExportGeneration generation,CancellationToken cancellation=default)
    {
        var verified=ReadableExportGeneration.Verify(generation.Manifest,generation.ManifestSha256,generation.Tables,cancellation);
        return ForTables(verified.Manifest.Source.GenerationId,verified.Manifest.Source.Watermark,verified.Tables,MaximumRequestBytes,cancellation);
    }

    internal static IEnumerable<GoogleSheetsValueBatch> ForTables(string generationId,long watermark,IEnumerable<ReadableExportTable> tables,int maximumBytes=MaximumRequestBytes,CancellationToken cancellation=default)
    {
        if(maximumBytes is <1024 or >MaximumRequestBytes)throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var verified=ReadableTableVerification.CopyAll(generationId,watermark,tables,new(cancellation:cancellation));
        int envelope=Serialize([]).Length,bytes=envelope;var pending=new List<SheetsWriteRectangle>();
        foreach(var table in verified)
        foreach(var rectangle in GoogleSheetsTableSlices.Build(table,maximumBytes-envelope-RangeEnvelopeReserve,cancellation))
        {
            cancellation.ThrowIfCancellationRequested();int length=JsonSerializer.SerializeToUtf8Bytes(ValueRange(rectangle)).Length;
            if(length+envelope>maximumBytes)throw Limit();
            if(pending.Count>0&&(bytes+length+1>maximumBytes||pending.Count==MaximumRanges))
            {
                yield return Freeze(generationId,watermark,pending,maximumBytes);pending=[];bytes=envelope;
            }
            bytes+=length+(pending.Count==0?0:1);pending.Add(rectangle);
        }
        if(pending.Count>0)yield return Freeze(generationId,watermark,pending,maximumBytes);
    }

    private static GoogleSheetsValueBatch Freeze(string generationId,long watermark,List<SheetsWriteRectangle> ranges,int maximumBytes)
    {
        var frozen=Array.AsReadOnly(ranges.ToArray());byte[] bytes=Serialize(frozen);if(bytes.Length>maximumBytes)throw Limit();return new(generationId,watermark,frozen,bytes);
    }
    private static object ValueRange(SheetsWriteRectangle rectangle)=>new{range=rectangle.Range,majorDimension="ROWS",values=rectangle.Values};
    private static byte[] Serialize(IReadOnlyList<SheetsWriteRectangle> ranges)=>JsonSerializer.SerializeToUtf8Bytes(new{valueInputOption="RAW",data=ranges.Select(ValueRange).ToArray(),includeValuesInResponse=false});
    private static InvalidDataException Limit()=>new("The literal Sheets request exceeds its serialized size budget. No truncated data can be sent.");
}
