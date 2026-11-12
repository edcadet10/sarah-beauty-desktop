using System.Text.Json;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CapturedDashboardFinanceTests
{
    [Fact]
    public void CapturedDashboardMatchesHumanFiguresAndUnknownMonthAfterLaterActivity()
    {
        using var f=new LedgerFixture();ApplicationTests.Invoice(f,5000,dueDate:new(2027,3,31));
        f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Expense,2500));
        f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,Date=new(2027,2,10)});
        f.Post(f.Draft(FinancialKind.LoanProceeds,2000) with{Date=new(2027,2,10)});
        var expected=new DashboardFinanceSources(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),new(2027,3,31));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Expense,500) with{Date=new(2027,3,10)});f.Business.Owner.Lock();
        var actual=source.Read(db=>DashboardFinanceSources.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,3,31),expected.Reports.GeneratedAtUtc));
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(actual));
        Assert.Equal(7500,actual.Months[0].ResultCents);Assert.Equal(10000,actual.Months[1].IncomeCents);
        Assert.Null(actual.Months[2].IncomeCents);Assert.False(actual.Months[2].HasPostedActivity);Assert.False(actual.Months[2].HasClosedCoverage);
        Assert.Equal(5000,actual.Aging.Sum(a=>a.Cents));Assert.Equal(5000,actual.Aging[0].Cents);
        Assert.Equal(source.Source.Watermark,actual.Reports.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void PublicLateLockIsRejectedAndCapturedWindowLimitsRemainEnforced()
    {
        using var f=new LedgerFixture();
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var human=new DashboardFinanceSources(f.Business.Store){AfterReportsRead=()=>f.Business.Owner.Lock()};
        Assert.Throws<UnauthorizedAccessException>(()=>human.Load(f.Business.Owner,f.Date,f.Date));
        var current=source.Read(db=>DashboardFinanceSources.Read(db,source.Source.BusinessId,f.Date,f.Date,source.Source.CapturedAtUtc));
        Assert.Equal(source.Source.CapturedAtUtc,current.Reports.GeneratedAtUtc);Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<ArgumentException>(()=>source.Read(db=>DashboardFinanceSources.Read(db,source.Source.BusinessId,new(2020,1,1),new(2030,1,31),source.Source.CapturedAtUtc)));
        Assert.Throws<ArgumentException>(()=>source.Read(db=>DashboardFinanceSources.Read(db,source.Source.BusinessId,f.Date.AddDays(1),f.Date,source.Source.CapturedAtUtc)));
    }
}
