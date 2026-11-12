using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableTaxTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private const string Private="Fictional-private-filing-reason-evidence";
    private static TaxPeriod Period(string jurisdiction)=>new(){Name="Fictional readable tax period",Jurisdiction=jurisdiction,From=new(2027,1,1),Through=new(2027,1,31)};

    [Fact]
    public void FilingZeroAndSignedAmendmentRemainSeparateFromUnknownPeriodsAndCash()
    {
        using var f=new LedgerFixture();
        var book=new TaxPeriodBook(f.Business.Store);
        var unknown=book.Save(f.Business.Owner,Period("Fictional unreviewed jurisdiction"));
        var period=book.Save(f.Business.Owner,Period("Fictional filed jurisdiction"));
        var filed=book.RecordStatus(f.Business.Owner,period.Id,new(Guid.NewGuid().ToString(),period.Revision,"Filed",new(2027,2,15),0,"Fictional return reference",Private,Private),BusinessFixture.Phrase);
        book.RecordStatus(f.Business.Owner,period.Id,new(Guid.NewGuid().ToString(),filed.Revision,"Amended",new(2027,2,16),-100,"Fictional amendment reference",Private,Private),BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableTaxTables.Build(source,new());
        Assert.Equal(2,tables.Length);
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var periods=tables.Single(t=>t.Name=="Tax filing periods");
        var pending=periods.Rows.Single(r=>r[0]==unknown.Id);
        Assert.Equal("Unreviewed",Cell(periods,pending,"Status"));
        Assert.Equal("",Cell(periods,pending,"DueDate"));
        Assert.Equal("Amended",Cell(periods,periods.Rows.Single(r=>r[0]==period.Id),"Status"));
        var history=tables.Single(t=>t.Name=="Tax filing history");
        Assert.Equal(2,history.Rows.Count);
        Assert.Equal("0",Cell(history,history.Rows[0],"ReportedTaxCents"));
        Assert.Equal("-100",Cell(history,history.Rows[1],"ReportedTaxCents"));
        Assert.Equal("2",Cell(history,history.Rows[1],"Sequence"));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void RemittanceDoesNotInventFilingAndReviewedNotRequiredKeepsDetailsUnknown()
    {
        using var f=new LedgerFixture();
        var book=new TaxPeriodBook(f.Business.Store);
        var unknown=book.Save(f.Business.Owner,Period("Fictional remittance jurisdiction"));
        var notRequired=book.Save(f.Business.Owner,Period("Fictional exempt jurisdiction"));
        book.RecordStatus(f.Business.Owner,notRequired.Id,new(Guid.NewGuid().ToString(),notRequired.Revision,"Not required",null,null,"",Private,Private),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableTaxTables.Build(source,new());
        var periods=tables.Single(t=>t.Name=="Tax filing periods");
        Assert.Equal("Unreviewed",Cell(periods,periods.Rows.Single(r=>r[0]==unknown.Id),"Status"));
        var history=tables.Single(t=>t.Name=="Tax filing history");
        var row=Assert.Single(history.Rows);
        Assert.Equal("Not required",Cell(history,row,"Status"));
        Assert.Equal("",Cell(history,row,"ReportedTaxCents"));
        Assert.Equal("",Cell(history,row,"FilingDate"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
        Assert.Equal(0,f.Balance("2000"));
    }
}
