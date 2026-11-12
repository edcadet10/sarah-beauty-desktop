using System.Text;

namespace SarahBeauty.Business.Exports;

internal static class ReadableExportMetadata
{
    internal static ReadableExportTable[] Build(ReadableExportAssembly assembly,ReadableExportBudget budget)
    {
        foreach(var table in assembly.Tables){budget.Consume(table.Columns);foreach(var row in table.Rows)budget.Consume(row);}
        return [StartHere(assembly,budget),Conventions(assembly.Source,budget),Inventory(assembly,budget)];
    }

    private static ReadableExportTable StartHere(ReadableExportAssembly assembly,ReadableExportBudget budget)
    {
        var source=assembly.Source;var dates=assembly.Selection;var table=new ReadableTableBuilder(source,budget,"Start Here","Property","Value");
        table.Add("Label","Export from Sarah Beauty Desktop");table.Add("AssemblyState","Local business tables assembled; remote publication is not established by these rows");
        table.Add("SourceFormatVersion",source.Version);table.Add("GenerationId",source.GenerationId);table.Add("BusinessId",source.BusinessId);
        table.Add("ProjectionFormatVersion",ReadableExportGeneration.FormatVersion);
        table.Add("BusinessName",source.BusinessName);table.Add("Mode",source.Mode);table.Add("StoreId",source.StoreId);table.Add("InstanceId",source.InstanceId);
        table.Add("SchemaVersion",source.SchemaVersion);table.Add("AppAssemblyVersion",typeof(ReadableExportAssembly).Assembly.GetName().Version?.ToString());
        table.Add("Watermark",source.Watermark);table.Add("CapturedAtUtc",source.CapturedAtUtc);table.Add("Currency","USD");
        table.Add("FinancialFrom",dates.FinancialFrom);table.Add("FinancialThrough",dates.FinancialThrough);table.Add("PlanningAsOf",dates.PlanningAsOf);
        table.Add("EventFrom",dates.EventFrom);table.Add("EventThrough",dates.EventThrough);table.Add("BusinessGroupCount",assembly.Groups.Count);table.Add("BusinessTableCount",assembly.Tables.Count);
        table.Add("Authority","Correct records in Sarah Beauty Desktop. Spreadsheet edits are not imported.");
        table.Add("ReportBasis","Management cash basis with approved book adjustments. Invoice dues, planning commitments, credits, and actual cash are separate facts.");
        table.Add("OwnerInterestStatus","Dated ownership confirmations and exact shares are included. See Ownership coverage at the selected planning date; unknown percentages remain unconfirmed. Actual owner movements do not establish percentage interests.");
        table.Add("InventoryScope","Business tables only. Metadata table hashes belong in the final generation manifest.");
        return table.Freeze();
    }

    private static ReadableExportTable Conventions(ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Export conventions","Convention","Meaning");
        table.Add("Literal cells","Every cell is literal text. Preserve identifiers, exact integers, and formula-like names without spreadsheet parsing.");
        table.Add("Money","Cents fields are exact integer USD cents. Divide by 100 for dollars; display formatting must not change the stored text.");
        table.Add("Decimals","Quantity, miles, fractions, and rates use invariant decimal text. A fraction of 0.075 means 7.5 percent.");
        table.Add("Blank cells","Blank can mean unknown, not applicable, not retained, or an empty optional text value. Read the column context and coverage tables; blank does not establish zero.");
        table.Add("Dates and instants","Calendar dates use Gregorian yyyy-MM-dd. UTC instants use ISO text with an explicit offset. Appointment rows retain their recorded time zone and offset separately.");
        table.Add("Identifiers","Join immutable IDs within their business and report scope. Display names and row numbers are not identifiers.");
        table.Add("Report scope","Captured-source reports use the selected window. Retained-period reports keep their original dates, observed watermark, and generation time, including after reopening.");
        table.Add("Record scope","Current register and history tables describe the captured database. The financial window does not filter every record table.");
        table.Add("Planning","Upcoming agreement gross is a planning commitment, not received cash. Known subtotals alongside unknown-value counts are not complete known totals.");
        table.Add("Cash and credits","Applications, held classifications, and credit funding do not create another cash receipt. Loan proceeds and owner contributions are separate from service income.");
        table.Add("Private omissions","Authentication, recovery keys, private notes, sensitivities, preferences, permission evidence, raw snapshots, and file binaries are excluded from readable tables.");
        table.Add("Ownership","Confirmation reasons and ownership evidence bindings are private. Captured corrections do not establish what ownership was known in a retained financial report. Percentages do not distribute cash, allocate taxable profit, or establish tax basis.");
        table.Add("File references","Recorded paths and hashes identify source versions. They do not prove that a file was uploaded, is accessible, or remains authorized for use.");
        table.Add("Integrity","A table digest binds its generation, watermark, name, schema, row order, and exact values. Local hashes do not establish remote verification or completeness.");
        return table.Freeze();
    }

    private static ReadableExportTable Inventory(ReadableExportAssembly assembly,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(assembly.Source,budget,"Business table inventory","Group","Table","DataRows","Columns","CellsIncludingHeader","RawUtf8BytesIncludingHeader","TableSha256");
        var names=assembly.Groups.SelectMany(g=>g.Tables.Select(t=>(Table:t,Group:g.Name))).ToDictionary(t=>t.Table,t=>t.Group,StringComparer.Ordinal);
        if(names.Count!=assembly.Tables.Count)throw new InvalidDataException("The readable group inventory does not cover its business tables.");
        foreach(var item in assembly.Tables)
        {
            if(!names.TryGetValue(item.Name,out string? group))throw new InvalidDataException("A readable business table is missing from the group inventory.");
            long bytes=item.Columns.Sum(c=>(long)Encoding.UTF8.GetByteCount(c))+item.Rows.Sum(r=>r.Sum(c=>(long)Encoding.UTF8.GetByteCount(c)));
            table.Add(group,item.Name,item.Rows.Count,item.Columns.Count,checked((item.Rows.Count+1L)*item.Columns.Count),bytes,item.Sha256);
        }
        return table.Freeze();
    }
}
