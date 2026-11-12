using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class BookingReportKernelTests
{
    private const string Private="Fictional-private-booking-report-note";
    private static BookingReportSnapshot Read(ReadableExportSnapshot source,string? business=null)=>source.Read(db=>BookingContributionReports.Read(db,business??source.Source.BusinessId,new(2027,1,1),new(2027,1,31),DocumentVersions.List(db,business??source.Source.BusinessId),source.Source.CapturedAtUtc));

    [Fact]
    public void CapturedChargesIncomeCashApplicationsAndDirectCostsMatchHumanReports()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var bookings=new BookingBook(f.Business.Store);var booking=bookings.List(f.Business.Owner).Single();
        bookings.Save(f.Business.Owner,booking with{PrivateNotes=Private});
        var tagged=f.Post(f.Draft(FinancialKind.Receipt,5000) with{BookingId=invoice.BookingId,FeeCents=100});
        var untagged=f.Post(f.Draft(FinancialKind.Receipt,2000));
        CancellationSourceTests.Apply(f,tagged.Id,invoice,3000);CancellationSourceTests.Apply(f,untagged.Id,invoice,2000);
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=invoice.BookingId});f.Post(f.Draft(FinancialKind.Expense,900));
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),new(2027,1,31)).Bookings!;
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Expense,1000) with{BookingId=invoice.BookingId});f.Business.Owner.Lock();
        var actual=Read(source);var row=Assert.Single(actual.Positions);var original=Assert.Single(expected.Positions);
        Assert.Equal(JsonSerializer.Serialize(original.CurrentFacts),JsonSerializer.Serialize(row.CurrentFacts));
        Assert.Equal(JsonSerializer.Serialize(original.Billing with{GeneratedAtUtc=source.Source.CapturedAtUtc}),JsonSerializer.Serialize(row.Billing));
        Assert.Equal(JsonSerializer.Serialize(original.Financial with{GeneratedAtUtc=source.Source.CapturedAtUtc}),JsonSerializer.Serialize(row.Financial));
        Assert.Equal(7900,row.ContributionEstimateCents);Assert.Equal(5000,row.OpenCents);Assert.Equal(2100,row.Financial.RecordedDirectCostCents);
        Assert.Equal(4900,row.Financial.Cash.Sum(c=>c.SignedNetCashCents));Assert.Equal(900,Assert.Single(actual.UnassignedDirectCosts).DebitCents);
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(actual));Assert.Equal(JsonSerializer.Serialize(actual),JsonSerializer.Serialize(Read(source)));
        Assert.Equal(source.Source.Watermark,row.Billing.Watermark);Assert.Equal(source.Source.Watermark,row.Financial.Watermark);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void UnissuedBookingKeepsUnknownContributionAndPrivateFactsStayOutOfForeignScope()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();
        var book=new BookingBook(f.Business.Store);
        var booking=book.Save(f.Business.Owner,BookingRules.Wedding("Fictional captured unissued event",1) with{PrimaryCustomerId=f.CustomerId,PrivateNotes=Private});
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=booking.Id});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var actual=Read(source);var row=Assert.Single(actual.Positions);
        Assert.Null(row.ContributionEstimateCents);Assert.Empty(row.Billing.Invoices);Assert.Equal(2000,row.Financial.RecordedDirectCostCents);
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(actual));
        var other=Read(source,foreign.Owner.BusinessId);Assert.Empty(other.Positions);Assert.Empty(other.UnassignedDirectCosts);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>new BookingBillingSources(f.Business.Store).Load(f.Business.Owner,booking.Id,f.Date));
        Assert.Throws<UnauthorizedAccessException>(()=>new BookingFinancialSources(f.Business.Store).Load(f.Business.Owner,booking.Id,f.Date,f.Date));
    }
}
