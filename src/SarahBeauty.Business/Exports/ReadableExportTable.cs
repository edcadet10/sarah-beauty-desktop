using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SarahBeauty.Business.Exports;

internal sealed record ReadableExportTable(string GenerationId,long Watermark,string Name,IReadOnlyList<string> Columns,IReadOnlyList<IReadOnlyList<string>> Rows,string Sha256);
internal sealed class ReadableExportBudget(long maximumCells=1_000_000,long maximumBytes=64*1024*1024,int maximumCellCharacters=32768,CancellationToken cancellation=default)
{
    private static readonly UTF8Encoding StrictUtf8=new(false,true);
    private long _cells,_bytes;
    internal void Consume(IReadOnlyList<string> values)
    {
        cancellation.ThrowIfCancellationRequested();
        if(maximumCells is <1 or >1_000_000 || maximumBytes is <1 or >64*1024*1024 || maximumCellCharacters is <1 or >32768)throw new ArgumentOutOfRangeException(nameof(maximumCells));
        long bytes=0;foreach(string value in values){if(value.Length>maximumCellCharacters || value.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t')))throw Limit();bytes=checked(bytes+ByteCount(value));}
        if(_cells+values.Count>maximumCells || _bytes+bytes>maximumBytes)throw Limit();_cells+=values.Count;_bytes+=bytes;
    }
    private static int ByteCount(string value)
    {
        try{return StrictUtf8.GetByteCount(value);}
        catch(EncoderFallbackException){throw new InvalidDataException("Readable exports require valid Unicode text; a replacement value cannot be published.");}
    }
    private static InvalidDataException Limit()=>new("The readable export exceeds its supported text/cell budget. No truncated generation can be published.");
}
internal sealed class ReadableTableBuilder
{
    private readonly string _generationId;
    private readonly long _watermark;
    private readonly ReadableExportBudget _budget;
    private readonly string _name;
    private readonly string[] _columns;
    private readonly List<IReadOnlyList<string>> _rows=[];
    private bool _finished;
    internal ReadableTableBuilder(ReadableExportSource source,ReadableExportBudget budget,string name,params string[] columns)
        :this(source.GenerationId,source.Watermark,budget,name,columns){}
    internal ReadableTableBuilder(string generationId,long watermark,ReadableExportBudget budget,string name,params string[] columns)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>64||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not(' ' or '_' or '-')) || columns.Length is <1 or >128 || columns.Any(c=>string.IsNullOrWhiteSpace(c)||c.Length>100||c.Any(char.IsControl)) || columns.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=columns.Length)throw new ArgumentException("Choose a supported unique readable table and column schema.");
        if(!Guid.TryParseExact(generationId,"N",out _)||watermark<0)throw new ArgumentException("A readable table needs its captured generation and watermark.");_generationId=generationId;_watermark=watermark;_budget=budget;_name=name;_columns=columns.ToArray();_budget.Consume(_columns);
    }
    internal void Add(params object?[] values)
    {
        if(_finished)throw new InvalidOperationException("The readable table is already frozen.");if(values.Length!=_columns.Length)throw new InvalidDataException("A readable export row does not match its explicit column schema.");string[] cells=values.Select(Cell).ToArray();_budget.Consume(cells);_rows.Add(Array.AsReadOnly(cells));
    }
    internal ReadableExportTable Freeze()
    {
        if(_finished)throw new InvalidOperationException("The readable table is already frozen.");_finished=true;var columns=Array.AsReadOnly(_columns);var rows=Array.AsReadOnly(_rows.ToArray());byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new{GenerationId=_generationId,Watermark=_watermark,Name=_name,Columns=columns,Rows=rows});return new(_generationId,_watermark,_name,columns,rows,Convert.ToHexString(SHA256.HashData(bytes)));
    }
    private static string Cell(object? value)=>value switch
    {
        null=>"",string text=>text,bool flag=>flag?"true":"false",int number=>number.ToString(CultureInfo.InvariantCulture),long number=>number.ToString(CultureInfo.InvariantCulture),decimal number=>number.ToString("G29",CultureInfo.InvariantCulture),BigInteger number=>number.ToString(CultureInfo.InvariantCulture),Guid id=>id.ToString(),DateOnly date=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),DateTimeOffset instant=>instant.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture),Enum option=>option.ToString(),_=>throw new InvalidDataException("Readable export columns accept explicit scalar facts only; nested records and binary values are not supported.")
    };
}
