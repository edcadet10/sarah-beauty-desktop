using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CapturedFinancialReportTests
{
    [Fact]
    public void CompleteCapturedReportMatchesHumanReportAfterLaterWriteAndLock()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=invoice.BookingId,FeeCents=300});
        CancellationSourceTests.Apply(f,receipt.Id,invoice,8000);
        var credits=new CreditBook(f.Business.Store);var credit=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional captured report credit",PaymentId=receipt.Id,Cents=2000,Date=f.Date});
        credits.Approve(f.Business.Owner,credit.Id,credit.Revision,BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Contribution,10000));f.Post(f.Draft(FinancialKind.LoanProceeds,5000));
        f.Post(f.Draft(FinancialKind.Expense,2500) with{BookingId=invoice.BookingId});
        var bill=SupplierPaymentTests.Bill(f);f.Post(SupplierPaymentTests.Payment(f,bill,17600));
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Expense,500));f.Business.Owner.Lock();
        ReportSnapshot Read()=>source.Read(db=>FinancialReports.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,1,31),expected.GeneratedAtUtc));
        var actual=Read();Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(actual),JsonSerializer.Serialize(Read()));
        Assert.Equal(source.Source.Watermark,actual.Watermark);Assert.Equal("TEST",actual.Mode);
        Assert.NotNull(actual.Assets);Assert.NotNull(actual.Debts);Assert.NotNull(actual.Owners);Assert.NotNull(actual.Bookings);Assert.NotNull(actual.TaxFilings);
        Assert.Equal(2000,actual.Customers.Single().UnusedCreditCents);Assert.Equal(17600,actual.SupplierBills.Single().PayableCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void HumanLockDuringReadIsRejectedAndAbsentSourceBusinessCannotProduceAReport()
    {
        using var f=new LedgerFixture();
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var report=new FinancialReports(f.Business.Store){AfterTrialRead=()=>f.Business.Owner.Lock()};
        Assert.Throws<UnauthorizedAccessException>(()=>report.Load(f.Business.Owner,f.Date,f.Date));
        var captured=source.Read(db=>FinancialReports.Read(db,source.Source.BusinessId,f.Date,f.Date,source.Source.CapturedAtUtc));
        Assert.Equal(source.Source.Watermark,captured.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<ArgumentException>(()=>source.Read(db=>FinancialReports.Read(db,Guid.NewGuid().ToString(),f.Date,f.Date,source.Source.CapturedAtUtc)));
        Assert.Throws<ArgumentException>(()=>source.Read(db=>FinancialReports.Read(db,source.Source.BusinessId,f.Date.AddDays(1),f.Date,source.Source.CapturedAtUtc)));
    }
}
