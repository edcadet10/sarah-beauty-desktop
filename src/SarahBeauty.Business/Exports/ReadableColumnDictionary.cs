namespace SarahBeauty.Business.Exports;

internal static class ReadableColumnDictionary
{
    private const string Name="Column dictionary";
    private static readonly string[] Columns=["Table","ColumnOrdinal","Column","Representation","Meaning","BlankMeaning","JoinTable","JoinColumn","JoinScope"];

    internal static ReadableExportTable Build(ReadableExportAssembly assembly,IEnumerable<ReadableExportTable> metadata,ReadableExportBudget budget)
    {
        var tables=ReadableTableVerification.CopyAll(assembly.Source,assembly.Tables.Concat(metadata),budget);
        var dictionary=new ReadableTableBuilder(assembly.Source,budget,Name,Columns);
        foreach(var table in tables)AddColumns(dictionary,table.Name,table.Columns,tables);
        AddColumns(dictionary,Name,Columns,tables);return dictionary.Freeze();
    }

    private static void AddColumns(ReadableTableBuilder dictionary,string table,IReadOnlyList<string> columns,IReadOnlyList<ReadableExportTable> tables)
    {
        for(int i=0;i<columns.Count;i++)
        {
            string column=columns[i];var join=ReadableColumnJoins.Target(table,column);
            if(join is {} target&&!tables.Any(t=>t.Name==target.Table&&t.Columns.Contains(target.Column)))throw new InvalidDataException("A readable dictionary join target is missing from its schema.");
            dictionary.Add(table,i+1,column,"Literal text",Meaning(table,column),
                "Unknown, not applicable, not retained, or optional empty text; see this table and its coverage rows. Never infer zero.",
                join?.Table,join?.Column,Scope(column));
        }
    }

    private static string Meaning(string table,string column)
    {
        if(column.Contains("Cents",StringComparison.Ordinal))return "Exact integer USD cents; signed where the source fact is signed.";
        if(column.EndsWith("TimeZoneId",StringComparison.Ordinal))return "Windows time-zone identifier; preserve the recorded appointment offset separately.";
        if(column.EndsWith("Id",StringComparison.Ordinal))return "Opaque identifier; preserve exact text and use its stated join scope.";
        if(column.EndsWith("Sha256",StringComparison.Ordinal))return "SHA-256 digest text identifying the recorded content.";
        if(column.EndsWith("AtUtc",StringComparison.Ordinal)||column is "StartUtc" or "EndUtc")return "ISO instant in UTC; preserve its explicit offset.";
        if(column is "RecordedStartWithOffset" or "RecordedEndWithOffset")return "ISO instant retaining the recorded appointment offset.";
        if(column.EndsWith("UtcOffset",StringComparison.Ordinal))return "Recorded UTC offset, separate from the time-zone identifier.";
        if(IsDate(table,column))return "Gregorian calendar date in yyyy-MM-dd; Month is the first date of its calendar month.";
        if(column.Contains("Fraction",StringComparison.Ordinal))return "Exact invariant decimal fraction; 0.075 means 7.5 percent.";
        if(column is "Quantity" or "ActualMiles")return "Exact invariant decimal quantity in the stated unit; ActualMiles is actual travel, separate from billed miles.";
        if(column.EndsWith("Minutes",StringComparison.Ordinal))return "Integer duration in minutes.";
        if(column=="ByteLength")return "Integer file size in bytes.";
        return "Recorded label, state, count, or text; interpret with this table's column name and scope.";
    }

    private static bool IsDate(string table,string column)=>
        (column.EndsWith("Date",StringComparison.Ordinal)||column.EndsWith("DateAtCapture",StringComparison.Ordinal)||
            column is "Through" or "Month" or "PlanningAsOf" or "AsOf" or "EventFrom" or "EventThrough" or "ClosedThrough" or "ReconciledThrough" or "PeriodStart" or "PeriodEnd" or "TrialDue" or "DepositDue" or "FinalDue" or "EffectiveFrom" or "EffectiveThrough"||
            column=="From"&&table!="Booking status history");

    private static string Scope(string column)=>column switch
    {
        "TimeZoneId" or "EventTimeZoneId"=>"Windows time-zone identifier; not a business-record foreign key.",
        "ReportId"=>"Use the associated report scope: captured GenerationId or original retained period. Preserve original dates, watermark, and time.",
        "SourceId" or "TargetId" or "DimensionId" or "ValueId"=>"Polymorphic identifier: read the adjacent kind/source/dimension before choosing a target; do not join by ID alone.",
        _=>"Join within the same BusinessId and report scope. A historical reference may have no current row. Do not replace frozen facts with current names or values."
    };
}
