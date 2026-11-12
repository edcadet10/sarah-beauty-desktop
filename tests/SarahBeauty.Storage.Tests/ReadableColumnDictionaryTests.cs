using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableColumnDictionaryTests
{
    [Fact]
    public void EveryBusinessMetadataAndDictionaryColumnHasOneOrderedEntry()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var selection=new ReadableExportSelection(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
        var assembly=ReadableExportAssembly.Build(source,selection);var metadata=ReadableExportMetadata.Build(assembly,new());
        var dictionary=ReadableColumnDictionary.Build(assembly,metadata,new());var tables=assembly.Tables.Concat(metadata).Append(dictionary).ToArray();
        Assert.Equal(tables.Sum(t=>t.Columns.Count),dictionary.Rows.Count);
        foreach(var table in tables)
        {
            var rows=dictionary.Rows.Where(r=>r[0]==table.Name).ToArray();Assert.Equal(table.Columns,rows.Select(r=>r[2]));
            Assert.Equal(Enumerable.Range(1,table.Columns.Count).Select(i=>i.ToString()),rows.Select(r=>r[1]));
        }
        var cents=Row(dictionary,"Journal lines","DebitCents");Assert.Contains("USD cents",cents[4]);
        Assert.Equal("Customers",Row(dictionary,"Invoice payers","CustomerId")[6]);
        Assert.Equal("Document versions",Row(dictionary,"Document lines","VersionId")[6]);
        Assert.Equal("Managed evidence version references",Row(dictionary,"Managed evidence links","VersionId")[6]);
        Assert.Equal("Customer credits",Row(dictionary,"Payment applications","CreditId")[6]);
        Assert.Equal("Supplier bill credits",Row(dictionary,"Supplier credit line reductions","CreditId")[6]);
        Assert.Equal("Bank match approvals",Row(dictionary,"Bank match portions","GroupId")[6]);
        Assert.Equal("Application approvals",Row(dictionary,"Payment applications","GroupId")[6]);
        Assert.Equal("Tax filing periods",Row(dictionary,"Report tax filing periods","PeriodId")[6]);
        Assert.Equal("Book period close history",Row(dictionary,"Ledger report scopes","RetainedPeriodId")[6]);
        Assert.DoesNotContain("calendar date",Row(dictionary,"Booking status history","From")[4]);
        Assert.Contains("calendar date",Row(dictionary,"Ledger report scopes","From")[4]);
        Assert.Equal("",Row(dictionary,"Managed evidence links","TargetId")[6]);
        Assert.Contains("Polymorphic",Row(dictionary,"Managed evidence links","TargetId")[8]);
        ReadableTableVerification.CopyAll(source.Source,tables,new());
    }

    [Fact]
    public void MissingJoinSchemaAndCombinedDictionaryBudgetPreventReturningADictionary()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var selection=new ReadableExportSelection(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
        var assembly=ReadableExportAssembly.Build(source,selection);var metadata=ReadableExportMetadata.Build(assembly,new());
        long cells=assembly.Tables.Concat(metadata).Sum(t=>(t.Rows.Count+1L)*t.Columns.Count);
        Assert.Throws<InvalidDataException>(()=>ReadableColumnDictionary.Build(assembly,metadata,new(maximumCells:cells)));
        var missing=assembly with{Tables=assembly.Tables.Where(t=>t.Name!="Owners").ToArray()};
        Assert.Throws<InvalidDataException>(()=>ReadableColumnDictionary.Build(missing,metadata,new()));
    }

    private static IReadOnlyList<string> Row(ReadableExportTable dictionary,string table,string column)=>dictionary.Rows.Single(r=>r[0]==table&&r[2]==column);
}
