using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class InvoiceVoidTaxTests
{
    [Fact]
    public void AFollowingMonthVoidReducesChargesInItsOwnPeriodAndCapturedReport()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000,.09m);InvoiceVoidTests.Book(f).VoidInvoice(f.Business.Owner,InvoiceVoidTests.Draft(invoice) with{DecisionDate=new(2027,2,1)},BusinessFixture.Phrase,Guid.NewGuid().ToString());
        var reports=new FinancialReports(f.Business.Store);var january=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31)).Tax;var february=reports.Load(f.Business.Owner,new(2027,2,1),new(2027,2,28)).Tax;var together=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,2,28)).Tax;
        Assert.Equal(450,january.ChargedCents);Assert.Equal(0,january.ChargeReductionCents);Assert.Equal(0,february.ChargedCents);Assert.Equal(450,february.ChargeReductionCents);Assert.Equal(450,together.ChargedCents);Assert.Equal(450,together.ChargeReductionCents);Assert.Equal(january.ChargedCents+february.ChargedCents,together.ChargedCents);Assert.Equal(january.ChargeReductionCents+february.ChargeReductionCents,together.ChargeReductionCents);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var table=ReadableTaxReportTables.Build(source,new(),new(2027,2,1),new(2027,2,28)).Single(t=>t.Name=="Report tax amounts");var row=Assert.Single(table.Rows);Assert.Equal("0",row[table.Columns.ToList().IndexOf("ChargedCents")]);Assert.Equal("450",row[table.Columns.ToList().IndexOf("ChargeReductionCents")]);
    }
}
