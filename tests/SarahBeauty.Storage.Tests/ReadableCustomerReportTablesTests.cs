using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableCustomerReportTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void PartialInvoicePaymentFundedCreditAndHeldDepositKeepExactSeparateValueAndTax()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m,dueDate:f.Date);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,15000) with{TaxCents=1500});
        var credits=new CreditBook(f.Business.Store);
        const string reason="Fictional-private-customer-value-reason";
        var draft=credits.SaveDraft(f.Business.Owner,new(){Name=reason,PaymentId=payment.Id,Cents=5000,Date=f.Date});
        var credit=credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        CancellationSourceTests.Apply(f,payment.Id,invoice,6000);
        f.Post(f.Draft(FinancialKind.Receipt,2000) with{CounterAccountId=f.Accounts["2200"].Id});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Receipt,1000));
        f.Business.Owner.Lock();
        var tables=ReadableCustomerReportTables.Build(source,new(),f.Date);
        Assert.Equal(4,tables.Length);
        Assert.DoesNotContain(reason,JsonSerializer.Serialize(tables));
        var balances=tables.Single(t=>t.Name=="Report customer balances");
        var balance=Assert.Single(balances.Rows);
        Assert.Equal("5000",Cell(balances,balance,"OpenCents"));
        Assert.Equal("4000",Cell(balances,balance,"UnallocatedRecognizedReceiptCents"));
        Assert.Equal("5000",Cell(balances,balance,"UnusedFundedCreditCents"));
        Assert.Equal("2000",Cell(balances,balance,"HeldReceiptCents"));
        var dues=tables.Single(t=>t.Name=="Report invoice amounts due");
        Assert.Equal(5000,dues.Rows.Sum(r=>long.Parse(Cell(dues,r,"Cents"))));
        Assert.Equal(455,dues.Rows.Sum(r=>long.Parse(Cell(dues,r,"TaxCents"))));
        var values=tables.Single(t=>t.Name=="Report available customer values");
        var funded=values.Rows.Single(r=>Cell(values,r,"ValueId")==credit.Id);
        Assert.Equal("FundedCredit",Cell(values,funded,"ValueKind"));
        Assert.Equal("500",Cell(values,funded,"AvailableTaxCents"));
        Assert.Equal("false",Cell(values,funded,"AdditionalCashReceipt"));
        var receipt=values.Rows.Single(r=>Cell(values,r,"ValueId")==payment.Id);
        Assert.Equal("455",Cell(values,receipt,"AvailableTaxCents"));
        Assert.Equal(3,values.Rows.Count);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedBalancesStayOriginalAndUnavailableHistoricalDetailIsMarkedExplicitly()
    {
        using var f=new LedgerFixture();
        BookPeriodTests.Ready(f);
        var invoice=ApplicationTests.Invoice(f,5000,dueDate:new(2027,2,10));
        var periods=new BookPeriods(f.Business.Store);
        var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,"Fictional report correction review",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var receipt=Assert.Single(f.Book.Posted(f.Business.Owner));
        CancellationSourceTests.Apply(f,receipt.Id,invoice,2000);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableCustomerReportTables.Build(source,new(),new(2027,1,31));
        var balances=tables.Single(t=>t.Name=="Report customer balances");
        Assert.Equal("5000",Cell(balances,balances.Rows.Single(r=>r[0]==closed.Id),"OpenCents"));
        Assert.Equal("3000",Cell(balances,balances.Rows.Single(r=>r[0]==source.Source.GenerationId),"OpenCents"));
        var coverage=tables.Single(t=>t.Name=="Customer report detail coverage");
        var retained=coverage.Rows.Single(r=>r[0]==closed.Id);
        Assert.Equal("Not retained",Cell(coverage,retained,"InvoiceMilestoneDetails"));
        Assert.Equal("Not retained",Cell(coverage,retained,"PaymentAndCreditDetails"));
        Assert.DoesNotContain(tables.Single(t=>t.Name=="Report invoice amounts due").Rows,r=>r[0]==closed.Id);
        Assert.DoesNotContain(tables.Single(t=>t.Name=="Report available customer values").Rows,r=>r[0]==closed.Id);
    }
}
