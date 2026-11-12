using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class TaxReportKernelTests
{
    private static TaxPosition Read(ReadableExportSnapshot source,DateOnly from,DateOnly through)=>source.Read(db=>TaxReportKernel.Read(db,source.Source.BusinessId,from,through,LedgerReportKernel.TrialBalance(db,source.Source.BusinessId,from,through),DocumentVersions.List(db,source.Source.BusinessId)));

    [Fact]
    public void ChargedCollectedRefundedAndRemittedTaxMatchDatedHumanReportsFromOneCapture()
    {
        using var f=new LedgerFixture();
        ApplicationTests.Invoice(f,10000,0.1m);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,Date=f.Date.AddMonths(1)});
        f.Post(f.Draft(FinancialKind.Refund,5500) with{OriginalId=payment.Id,TaxCents=500,Date=f.Date.AddMonths(2)});
        f.Post(f.Draft(FinancialKind.TaxRemittance,500) with{Date=f.Date.AddMonths(3)});
        var expected=Enumerable.Range(1,4).Select(month=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month))).Tax).ToArray();
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Receipt,1100) with{TaxCents=100,Date=f.Date.AddMonths(3).AddDays(1)});
        f.Business.Owner.Lock();
        for(int month=1;month<=4;month++)Assert.Equal(expected[month-1],Read(source,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month))));
        Assert.Equal(1000,expected[0].ChargedCents);Assert.Equal(0,expected[0].CollectedCents);
        Assert.Equal(1000,expected[1].CollectedCents);Assert.Equal(500,expected[2].RefundedCents);
        Assert.Equal(500,expected[3].RemittedCents);Assert.Equal(0,expected[3].LiabilityCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RecoveryWriteOffAndFilingFactsRemainSeparateWithoutRenewingHumanAccess()
    {
        using var f=new LedgerFixture();
        var payment=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        f.Post(f.Draft(FinancialKind.TaxRemittance,800));
        var refund=f.Post(f.Draft(FinancialKind.Refund,11000) with{OriginalId=payment.Id,TaxCents=1000,TaxRefundAllocation=new(200,300,500),EvidenceId="Fictional reviewed refund tax allocation"});
        f.Post(f.Draft(FinancialKind.TaxRecovery,300) with{OriginalId=refund.Id,TaxRecoveryDestination=TaxRecoveryDestination.WriteOff,EvidenceId="Fictional reviewed recovery write off"});
        var periods=new TaxPeriodBook(f.Business.Store);
        var period=periods.Save(f.Business.Owner,new(){Name="Fictional scoped return",Jurisdiction="Fictional jurisdiction",From=new(2027,1,1),Through=new(2027,1,31)});
        periods.RecordStatus(f.Business.Owner,period.Id,new(Guid.NewGuid().ToString(),period.Revision,"Not required",null,null,"","Fictional reviewed filing evidence","Fictional filing decision"),BusinessFixture.Phrase);
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Business.Owner.Lock();
        Assert.Equal(expected.Tax,Read(source,new(2027,1,1),new(2027,1,31)));
        Assert.Equal(800,expected.Tax.NonrecoverableCents);Assert.Equal(0,expected.Tax.RecoverableCents);
        Assert.Equal(0,expected.Tax.RecoveredCashCents);Assert.Equal(0,expected.ProfitLoss.IncomeCents);
        var actual=source.Read(db=>TaxFilingReports.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,1,31)));
        var filed=Assert.Single(actual.Periods);var original=Assert.Single(expected.TaxFilings!.Periods);
        Assert.Equal(original.Id,filed.Id);Assert.Equal(original.History,filed.History);
        Assert.Equal("Not required",filed.StatusAtCapture);Assert.Null(filed.DueDate);
        Assert.Null(filed.History.Single().Decision.ReportedTaxCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date));
    }
}
