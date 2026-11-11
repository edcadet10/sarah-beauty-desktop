using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class DebtScheduleTests
{
    private static ReportSnapshot Report(LedgerFixture f,int month)=>new FinancialReports(f.Business.Store)
        .Load(f.Business.Owner,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)));

    [Fact]
    public void ProceedsPrincipalInterestFeesAndLaterReversalUseTheirOwnPeriods()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.LoanProceeds,30000));
        var payment=f.Post(f.Draft(FinancialKind.LoanPayment,11200) with{PrincipalCents=10000,InterestCents=1000,FeeCents=200,Date=new(2027,2,10)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id,Date=new(2027,3,10)});
        var january=Report(f,1);var jan=Assert.Single(january.Debts!.Positions);
        Assert.Equal(0,jan.PostedOpeningPrincipalCents);Assert.Equal(30000,jan.ProceedsCents);Assert.Equal(30000,jan.ClosingPrincipalCents);
        Assert.Equal(0,january.ProfitLoss.IncomeCents);
        var february=Report(f,2);var feb=Assert.Single(february.Debts!.Positions);
        Assert.Equal(30000,feb.PostedOpeningPrincipalCents);Assert.Equal(10000,feb.PrincipalPaidCents);Assert.Equal(20000,feb.ClosingPrincipalCents);
        Assert.Equal(1000,feb.InterestCents);Assert.Equal(200,feb.FeesCents);Assert.Equal(1200,february.ProfitLoss.ExpenseCents);
        var march=Report(f,3);var restored=Assert.Single(march.Debts!.Positions);
        Assert.Equal(20000,restored.PostedOpeningPrincipalCents);Assert.Equal(-10000,restored.PrincipalPaidCents);
        Assert.Equal(-1000,restored.InterestCents);Assert.Equal(-200,restored.FeesCents);Assert.Equal(30000,restored.ClosingPrincipalCents);
        Assert.Equal(-march.TrialBalance.Single(t=>t.Account.Code=="2300").Ending,restored.ClosingPrincipalCents);
        Assert.Contains(march.Debts.Entries,e=>e.Kind=="Reversal" && e.EffectiveKind=="LoanPayment" && e.AccountCode=="2300");
    }

    [Fact]
    public void RegisteredOpeningIsNotPostedPrincipalAndFutureOpeningDoesNotLeakBackward()
    {
        using var f=new LedgerFixture();var resources=new BusinessResources(f.Business.Store);
        var loan=resources.SaveLoan(f.Business.Owner,f.Loan with{OpeningDate=new(2027,2,1),OpeningPrincipalCents=10000,DueDate=new(2027,12,1)});
        var opening=f.Post(f.Draft(FinancialKind.Opening,10000) with{LoanId=loan.Id,CounterAccountId=f.Accounts["2300"].Id,OpeningCredit=true,
            Date=new(2027,2,1),EvidenceId="Fictional approved opening evidence"});
        Assert.Null(Assert.Single(Report(f,1).Debts!.Positions).ClosingPrincipalCents);
        var feb=Assert.Single(Report(f,2).Debts!.Positions);
        Assert.Equal(0,feb.PostedOpeningPrincipalCents);Assert.Equal(10000,feb.OpeningAdjustmentsCents);Assert.Equal(0,feb.ProceedsCents);
        Assert.Equal(10000,feb.ClosingPrincipalCents);Assert.Equal(loan.DueDate,feb.CurrentFacts.DueDate);
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=opening.Id,Date=new(2027,3,1)});
        var march=Assert.Single(Report(f,3).Debts!.Positions);
        Assert.Equal(-10000,march.OpeningAdjustmentsCents);Assert.Equal(0,march.ClosingPrincipalCents);
    }

    [Fact]
    public void SeparateArchivedLoansReconcileAndRetainCapturedLendersAfterCurrentNamesChange()
    {
        using var f=new LedgerFixture();var resources=new BusinessResources(f.Business.Store);
        var loan=resources.SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        var other=resources.SaveLoan(f.Business.Owner,new Loan{Name="Fictional external loan",OpeningPrincipalCents=0});
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        f.Post(f.Draft(FinancialKind.LoanProceeds,20000) with{LoanId=other.Id});
        f.Post(f.Draft(FinancialKind.LoanPayment,10000) with{PrincipalCents=10000});
        var frozen=Report(f,1);string json=JsonSerializer.Serialize(frozen);
        resources.SaveLoan(f.Business.Owner,loan with{Name="Fictional current name",Archived=true,Terms="Fictional revised terms"});
        var latest=Report(f,1);var paid=latest.Debts!.Positions.Single(p=>p.CurrentFacts.LoanId==loan.Id);
        Assert.True(paid.CurrentFacts.Archived);Assert.Equal(0,paid.ClosingPrincipalCents);Assert.Equal(20000,latest.Debts.Positions.Sum(p=>p.ClosingPrincipalCents));
        Assert.All(latest.Debts.Entries.Where(e=>e.LoanId==loan.Id),e=>{Assert.Equal(loan.Name,e.CapturedLender!.LenderName);Assert.Equal(f.Business.Owner.OwnerId,e.CapturedLender.OwnerLenderId);});
        Assert.Equal(loan.Name,JsonSerializer.Deserialize<ReportSnapshot>(json)!.Debts!.Positions.Single(p=>p.CurrentFacts.LoanId==loan.Id).CurrentFacts.Name);
        Assert.Equal(-latest.TrialBalance.Single(t=>t.Account.Code=="2300").Ending,latest.Debts.Positions.Sum(p=>p.ClosingPrincipalCents));
    }

    [Fact]
    public void UnpostedAndLegacySnapshotsStayDistinctFromKnownZeroAndConcurrentChanges()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");
        var initial=Report(f,1);Assert.Null(Assert.Single(initial.Debts!.Positions).ClosingPrincipalCents);
        var legacy=JsonSerializer.SerializeToNode(initial)!.AsObject();legacy.Remove("Debts");
        Assert.Null(JsonSerializer.Deserialize<ReportSnapshot>(legacy.ToJsonString())!.Debts);
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        var reports=new FinancialReports(f.Business.Store){AfterTrialRead=()=>f.Post(f.Draft(FinancialKind.LoanProceeds,20000))};
        var consistent=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));
        Assert.Equal(10000,Assert.Single(consistent.Debts!.Positions).ClosingPrincipalCents);
        Assert.Equal(-10000,consistent.TrialBalance.Single(t=>t.Account.Code=="2300").Ending);
        Assert.Equal(-30000,f.Balance("2300"));
    }

    [Fact]
    public void OlderApprovedMovementDoesNotAcquireLenderAttributionFromCurrentRegister()
    {
        using var f=new LedgerFixture();
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000));
        f.Business.Store.Write(f.Business.Owner,"Fictional older loan writer probe",draft.Id,db=>
            PostingWriter.Save(db,f.Business.Owner,draft with{LoanLender=null},PostingRules.Build(draft,f.Accounts.Values.ToList()),null));
        var report=Report(f,1);
        Assert.Equal(f.Business.Owner.OwnerId,Assert.Single(report.Debts!.Positions).CurrentFacts.OwnerLenderId);
        Assert.Equal("Alex",Assert.Single(report.Debts.Positions).CurrentFacts.OwnerLenderName);
        Assert.All(report.Debts.Entries,e=>Assert.Null(e.CapturedLender));
        Assert.Equal(10000,Assert.Single(report.Debts.Positions).ClosingPrincipalCents);
    }
}
