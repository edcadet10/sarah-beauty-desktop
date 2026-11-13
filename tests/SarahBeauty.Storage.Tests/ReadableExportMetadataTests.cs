using System.Text;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableExportMetadataTests
{
    private static readonly ReadableExportSelection Dates=new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));

    [Fact]
    public void SourceSelectionsAndEveryBusinessTableHaveExplicitReadableMetadata()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var assembly=ReadableExportAssembly.Build(source,Dates);var metadata=ReadableExportMetadata.Build(assembly,new());
        var start=metadata.Single(t=>t.Name=="Start Here").Rows.ToDictionary(r=>r[0],r=>r[1]);
        Assert.Equal(source.Source.GenerationId,start["GenerationId"]);Assert.Equal("TEST",start["Mode"]);Assert.Equal("USD",start["Currency"]);
        Assert.Equal("2027-01-01",start["FinancialFrom"]);Assert.Equal("2027-03-31",start["EventThrough"]);Assert.Equal("159",start["BusinessTableCount"]);Assert.Equal("2",start["ProjectionFormatVersion"]);
        Assert.Contains("unknown percentages remain unconfirmed",start["OwnerInterestStatus"]);Assert.Contains("remote publication is not established",start["AssemblyState"]);
        var inventory=metadata.Single(t=>t.Name=="Business table inventory");Assert.Equal(159,inventory.Rows.Count);
        foreach(var table in assembly.Tables)
        {
            var row=inventory.Rows.Single(r=>r[1]==table.Name);Assert.Equal(table.Rows.Count.ToString(),row[2]);Assert.Equal(table.Columns.Count.ToString(),row[3]);
            Assert.Equal(((table.Rows.Count+1L)*table.Columns.Count).ToString(),row[4]);
            long bytes=table.Columns.Sum(c=>(long)Encoding.UTF8.GetByteCount(c))+table.Rows.Sum(r=>r.Sum(c=>(long)Encoding.UTF8.GetByteCount(c)));
            Assert.Equal(bytes.ToString(),row[5]);Assert.Equal(table.Sha256,row[6]);
        }
        Assert.Equal(metadata.Select(t=>t.Sha256),ReadableExportMetadata.Build(assembly,new()).Select(t=>t.Sha256));
        ReadableTableVerification.CopyAll(source.Source,assembly.Tables.Concat(metadata),new());
    }

    [Fact]
    public void MetadataConsumesTheSameBudgetAsBusinessTablesAndRejectsBrokenInventory()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var assembly=ReadableExportAssembly.Build(source,Dates);
        long cells=assembly.Tables.Sum(t=>(t.Rows.Count+1L)*t.Columns.Count);
        Assert.Throws<InvalidDataException>(()=>ReadableExportMetadata.Build(assembly,new(maximumCells:cells)));
        var broken=assembly with{Groups=assembly.Groups.Skip(1).ToArray()};
        Assert.Throws<InvalidDataException>(()=>ReadableExportMetadata.Build(broken,new()));
    }
}
