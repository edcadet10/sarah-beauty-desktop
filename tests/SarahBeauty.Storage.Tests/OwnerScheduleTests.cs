using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerScheduleTests
{
    private static ReportSnapshot Report(LedgerFixture f,int month=1)=>new FinancialReports(f.Business.Store)
        .Load(f.Business.Owner,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)));

    [Fact]
    public void ActualOwnersKeepContributionsDrawsReimbursementsAndLoansSeparate()
    {
        using var f=new LedgerFixture();string alex=f.Business.Owner.OwnerId;
        string morgan=new OwnerAccess(f.Business.Store).Owners().Single(o=>o.Id!=alex).Id;
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=alex});
        f.Post(f.Draft(FinancialKind.Contribution,10000));
        f.Post(f.Draft(FinancialKind.Contribution,5000) with{EconomicOwnerId=morgan});
        f.Post(f.Draft(FinancialKind.Withdrawal,2000));
        f.Post(f.Draft(FinancialKind.AssetContribution,3000));
        f.Post(f.Draft(FinancialKind.OwnerPaidExpense,4000));
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,1500));
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        f.Post(f.Draft(FinancialKind.LoanPayment,2600) with{PrincipalCents=2000,InterestCents=500,FeeCents=100});
        f.Business.Store.Write(f.Business.Owner,"Fictional inactive owner report probe",morgan,db=>db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",morgan)));
        var report=Report(f);var first=report.Owners!.Positions.Single(o=>o.OwnerId==alex);var second=report.Owners.Positions.Single(o=>o.OwnerId==morgan);
        Assert.Equal(13000,first.ContributionsCents);Assert.Equal(2000,first.DrawsCents);
        Assert.Equal(4000,first.PayableAddedCents);Assert.Equal(1500,first.PayableClearedCents);Assert.Equal(1500,first.ReimbursementsCents);
        Assert.Equal(10000,first.LoanProceedsCents);Assert.Equal(2000,first.LoanPrincipalPaidCents);Assert.Equal(8000,first.AttributedLoanPrincipalCents);
        Assert.Equal(5000,second.ContributionsCents);Assert.Equal(0,second.DrawsCents);Assert.Equal(0,second.AttributedLoanPrincipalCents);Assert.False(second.Active);
        foreach(string code in new[]{"3000","3100","2100"})Assert.Equal(report.TrialBalance.Single(t=>t.Account.Code==code).Ending,
            report.Owners.Accounts.Where(a=>a.AccountCode==code).Sum(a=>a.ClosingDebitLessCreditCents));
        Assert.Equal(4600,report.ProfitLoss.ExpenseCents);
    }

    [Fact]
    public void EvidencedOpeningsAreSeparateFromNewContributionsAndReversalsUseTheirOwnPeriod()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.Opening,2500) with{EconomicOwnerId=f.Business.Owner.OwnerId,CounterAccountId=f.Accounts["3000"].Id,
            OpeningCredit=true,Date=new(2026,12,15),EvidenceId="Fictional prior capital opening"});
        var added=f.Post(f.Draft(FinancialKind.Contribution,3000));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=added.Id,Date=new(2027,2,10)});
        var january=Report(f);var account=january.Owners!.Accounts.Single(a=>a.OwnerId==f.Business.Owner.OwnerId && a.AccountCode=="3000");
        Assert.Equal(-2500,account.OpeningDebitLessCreditCents);Assert.Equal(3000,account.PeriodCreditCents);Assert.Equal(-5500,account.ClosingDebitLessCreditCents);
        Assert.Equal(3000,january.Owners.Positions.Single(o=>o.OwnerId==f.Business.Owner.OwnerId).ContributionsCents);
        var february=Report(f,2);Assert.Equal(-3000,february.Owners!.Positions.Single(o=>o.OwnerId==f.Business.Owner.OwnerId).ContributionsCents);
        Assert.Equal(-2500,february.Owners.Accounts.Single(a=>a.OwnerId==f.Business.Owner.OwnerId && a.AccountCode=="3000").ClosingDebitLessCreditCents);
        Assert.Equal(0,february.ProfitLoss.IncomeCents);
    }

    [Fact]
    public void MixedPurchaseFundingKeepsBusinessPayableAndPersonalBenefitWithActualOwners()
    {
        using var f=new LedgerFixture();string morgan=new OwnerAccess(f.Business.Store).Owners().Single(o=>o.Id!=f.Business.Owner.OwnerId).Id;
        var reimbursable=SupplierPaymentTests.Bill(f);
        f.Post(SupplierPaymentTests.Payment(f,reimbursable,35200,"Owner reimbursable") with{CashAccountId=""});
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,10000));
        var cash=SupplierPaymentTests.Bill(f);
        f.Post(SupplierPaymentTests.Payment(f,cash,35200) with{EconomicOwnerId=morgan});
        var report=Report(f);var owner=report.Owners!.Positions.Single(o=>o.OwnerId==f.Business.Owner.OwnerId);
        Assert.Equal(34100,owner.PayableAddedCents);Assert.Equal(10000,owner.ReimbursementsCents);Assert.Equal(0,owner.ContributionsCents);
        Assert.Equal(1100,report.Owners.Positions.Single(o=>o.OwnerId==morgan).DrawsCents);
        Assert.Equal(-24100,report.Owners.Accounts.Single(a=>a.OwnerId==owner.OwnerId && a.AccountCode=="2100").ClosingDebitLessCreditCents);
    }

    [Fact]
    public void LegacyLoanBalanceIsNotAssignedFromCurrentRegisterEvenWithNewCapturedRepayment()
    {
        using var f=new LedgerFixture();
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000) with{EconomicOwnerId=f.Business.Owner.OwnerId});
        f.Business.Store.Write(f.Business.Owner,"Fictional old lender attribution probe",draft.Id,db=>
            PostingWriter.Save(db,f.Business.Owner,draft with{LoanLender=null},PostingRules.Build(draft,f.Accounts.Values.ToList()),null));
        f.Post(f.Draft(FinancialKind.LoanPayment,3000) with{PrincipalCents=3000});
        var report=Report(f);var owner=report.Owners!.Positions.Single(o=>o.OwnerId==f.Business.Owner.OwnerId);var loan=Assert.Single(report.Owners.LoanBalances);
        Assert.Equal(3000,owner.LoanPrincipalPaidCents);Assert.Equal(0,owner.AttributedLoanPrincipalCents);Assert.Equal(0,owner.ContributionsCents);
        Assert.Empty(loan.OwnerId);Assert.Equal("Historical lender attribution unconfirmed",loan.Attribution);Assert.Equal(7000,loan.PrincipalCents);
        Assert.DoesNotContain(report.Owners.Entries,e=>e.Kind=="LoanProceeds");
    }

    [Fact]
    public void ConcurrentOwnerAndLoanPostingsDoNotMixSnapshotsAndOlderReportsRemainWithoutSchedule()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        f.Post(f.Draft(FinancialKind.Contribution,10000));
        var reports=new FinancialReports(f.Business.Store){AfterTrialRead=()=>{f.Post(f.Draft(FinancialKind.Contribution,20000));f.Post(f.Draft(FinancialKind.LoanProceeds,5000));}};
        var report=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));var owner=report.Owners!.Positions.Single(o=>o.OwnerId==f.Business.Owner.OwnerId);
        Assert.Equal(10000,owner.ContributionsCents);Assert.Equal(0,owner.LoanProceedsCents);Assert.Equal(0,owner.AttributedLoanPrincipalCents);
        Assert.Equal(-10000,report.TrialBalance.Single(t=>t.Account.Code=="3000").Ending);Assert.Equal(-30000,f.Balance("3000"));
        var legacy=JsonSerializer.SerializeToNode(report)!.AsObject();legacy.Remove("Owners");
        Assert.Null(JsonSerializer.Deserialize<ReportSnapshot>(legacy.ToJsonString())!.Owners);
        Assert.Equal(1,report.Owners.Version);
    }
}
