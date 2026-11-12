using System.Text;
using System.Text.Json;

namespace SarahBeauty.Business.Exports;

internal static class ReadableTableStreams
{
    internal const long MaximumFileBytes=512L*1024*1024;
    internal const int MaximumLineBytes=26*1024*1024;

    internal static void Write(Stream destination,ReadableExportTable table,CancellationToken cancellation=default)
    {
        if(!destination.CanWrite||!destination.CanSeek||destination.Length!=0||destination.Position!=0)throw new ArgumentException("Use a new empty seekable stream for a staged readable table.");
        var budget=new ReadableExportBudget(cancellation:cancellation);long written=0;
        WriteLine(table.Columns);foreach(var row in table.Rows){if(row.Count!=table.Columns.Count)throw Invalid();WriteLine(row);}
        void WriteLine(IReadOnlyList<string> values)
        {
            if(values.Count is <1 or >128||values.Any(v=>v is null))throw Invalid();budget.Consume(values);
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(values);
            if(bytes.Length>MaximumLineBytes||written+bytes.Length+1>MaximumFileBytes)throw Invalid();
            destination.Write(bytes);destination.WriteByte((byte)'\n');written+=bytes.Length+1;
        }
    }

    internal static ReadableExportTable Read(Stream source,ReadableGenerationSource identity,ReadableTableFacts expected,ReadableExportBudget budget,CancellationToken cancellation=default)
    {
        if(!source.CanRead||!source.CanSeek||source.Position!=0||source.Length>MaximumFileBytes||expected.Columns is <1 or >128||expected.DataRows is <0 or >1_000_000||
            expected.Cells!=checked((expected.DataRows+1L)*expected.Columns)||expected.RawUtf8Bytes is <0 or >64*1024*1024||expected.Sha256 is not{Length:64})throw Invalid();
        var reader=new ReadableJsonLines(source);string[] columns=Cells(reader.Read(MaximumLineBytes,cancellation));
        if(columns.Length!=expected.Columns)throw Invalid();var builder=new ReadableTableBuilder(identity.GenerationId,identity.Watermark,budget,expected.Name,columns);
        for(int i=0;i<expected.DataRows;i++){cancellation.ThrowIfCancellationRequested();builder.Add(Cells(reader.Read(MaximumLineBytes,cancellation)).Cast<object?>().ToArray());}
        if(reader.Read(MaximumLineBytes,cancellation) is not null)throw Invalid();
        var table=builder.Freeze();if(ReadableGenerationFacts.Table(table)!=expected)throw Invalid();return table;
    }

    private static string[] Cells(byte[]? line)
    {
        if(line is null)throw Invalid();
        try
        {
            var reader=new Utf8JsonReader(line,new JsonReaderOptions{MaxDepth=2});
            if(!reader.Read()||reader.TokenType!=JsonTokenType.StartArray)throw Invalid();var cells=new List<string>();
            while(reader.Read()&&reader.TokenType!=JsonTokenType.EndArray)
            {
                if(reader.TokenType!=JsonTokenType.String||cells.Count==128)throw Invalid();string value=reader.GetString()!;
                if(value.Length>32768)throw Invalid();cells.Add(value);
            }
            if(reader.TokenType!=JsonTokenType.EndArray||reader.Read())throw Invalid();return cells.ToArray();
        }
        catch(Exception error)when(error is JsonException or InvalidOperationException or DecoderFallbackException){throw Invalid();}
    }

    private static InvalidDataException Invalid()=>new("The staged readable table has unsupported content, size, shape, or recorded facts.");
}
