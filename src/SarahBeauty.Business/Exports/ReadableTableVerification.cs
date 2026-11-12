namespace SarahBeauty.Business.Exports;

internal static class ReadableTableVerification
{
    internal static IReadOnlyList<ReadableExportTable> CopyAll(ReadableExportSource source,IEnumerable<ReadableExportTable> tables,ReadableExportBudget budget)
        =>CopyAll(source.GenerationId,source.Watermark,tables,budget);

    internal static IReadOnlyList<ReadableExportTable> CopyAll(string generationId,long watermark,IEnumerable<ReadableExportTable> tables,ReadableExportBudget budget)
    {
        var result=new List<ReadableExportTable>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var table in tables)
        {
            if(result.Count>=256||!names.Add(table.Name))throw Invalid();
            result.Add(Copy(generationId,watermark,table,budget));
        }
        if(result.Count==0)throw Invalid();return result.AsReadOnly();
    }

    private static ReadableExportTable Copy(string generationId,long watermark,ReadableExportTable table,ReadableExportBudget budget)
    {
        if(table.GenerationId!=generationId||table.Watermark!=watermark)throw Invalid();
        var builder=new ReadableTableBuilder(generationId,watermark,budget,table.Name,table.Columns.ToArray());
        foreach(var row in table.Rows)
        {
            if(row.Count!=table.Columns.Count)throw Invalid();
            builder.Add(row.Cast<object?>().ToArray());
        }
        var copy=builder.Freeze();if(copy.Sha256!=table.Sha256)throw Invalid();return copy;
    }

    private static InvalidDataException Invalid()=>new("Readable tables must have unique names, valid content hashes, and one captured generation and watermark.");
}
