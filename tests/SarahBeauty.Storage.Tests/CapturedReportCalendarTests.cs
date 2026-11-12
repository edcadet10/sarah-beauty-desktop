using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CapturedReportCalendarTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void StoredIsoReportDatesDoNotDependOnTheCurrentCalendar(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});CancellationSourceTests.Apply(f,receipt.Id,invoice,5500);
        f.Post(f.Draft(FinancialKind.LoanProceeds,5000));f.Post(f.Draft(FinancialKind.Expense,1000));
        f.Post(f.Draft(FinancialKind.AssetPurchase,3000));f.Post(f.Draft(FinancialKind.Depreciation,500));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        ReportSnapshot Read()=>source.Read(db=>FinancialReports.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,1,31),source.Source.CapturedAtUtc));
        string expected=JsonSerializer.Serialize(Read());var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            Assert.NotEqual("2027-01-10",f.Date.ToString("yyyy-MM-dd"));
            Assert.Equal(expected,JsonSerializer.Serialize(Read()));
        }
        finally{CultureInfo.CurrentCulture=previous;}
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ClosedReportsAndDashboardPreserveTheirCapturedDates(string culture)
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        BookPeriodTests.Close(f,new BookPeriods(f.Business.Store));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        string Read()=>JsonSerializer.Serialize(new
        {
            Dashboard=source.Read(db=>DashboardFinanceSources.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,1,31),source.Source.CapturedAtUtc)),
            History=ReadableBookHistory.Build(source,new()),
            Reconciliation=ReadableReconciliationTables.Build(source,new()),
            Reports=ReadableLedgerReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31))
        });
        AssertCalendarIndependent(culture,Read);
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void UpcomingPlanningKeepsDatedMilestonePayments(string culture)
    {
        using var f=new LedgerFixture();var ready=ReservationTests.Setup(f);
        ReservationTests.Pay(f,ready.Invoice,22500);ReservationTests.Confirm(f,ready.Booking);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        string Read()=>JsonSerializer.Serialize(source.Read(db=>DashboardUpcomingSources.Read(db,source.Source.BusinessId,
            new(2027,1,31),new(2027,3,1),new(2027,3,31),source.Source.CapturedAtUtc)));
        AssertCalendarIndependent(culture,Read);
    }

    private static void AssertCalendarIndependent(string culture,Func<string> read)
    {
        string expected=read();var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);Assert.Equal(expected,read());}
        finally{CultureInfo.CurrentCulture=previous;}
    }
}
