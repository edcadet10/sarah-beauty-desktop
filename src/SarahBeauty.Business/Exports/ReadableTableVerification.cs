namespace SarahBeauty.Business.Exports;

internal static class ReadableTableVerification
{
    internal static IReadOnlyList<ReadableExportTable> CopyAll(ReadableExportSource source,IEnumerable<ReadableExportTable> tables,ReadableExportBudget budget)
    {
        var result=new List<ReadableExportTable>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var table in tables)
        {
            if(result.Count>=256||!names.Add(table.Name))throw Invalid();
            result.Add(Copy(source,table,budget));
        }
        if(result.Count==0)throw Invalid();return result.AsReadOnly();
    }

    private static ReadableExportTable Copy(ReadableExportSource source,ReadableExportTable table,ReadableExportBudget budget)
    {
        if(table.GenerationId!=source.GenerationId||table.Watermark!=source.Watermark)throw Invalid();
        var builder=new ReadableTableBuilder(source,budget,table.Name,table.Columns.ToArray());
        foreach(var row in table.Rows)
        {
            if(row.Count!=table.Columns.Count)throw Invalid();
            builder.Add(row.Cast<object?>().ToArray());
        }
        var copy=builder.Freeze();if(copy.Sha256!=table.Sha256)throw Invalid();return copy;
    }

    private static InvalidDataException Invalid()=>new("Readable tables must have unique names, valid content hashes, and one captured generation and watermark.");
}
