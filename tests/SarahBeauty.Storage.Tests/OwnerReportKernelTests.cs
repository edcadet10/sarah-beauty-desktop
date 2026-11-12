using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerReportKernelTests
{
    [Fact]
    public void CapturedOwnerMovementsMatchHumanReportsWithoutRenewingAccessOrDistributingProfit()
    {
        using var f=new LedgerFixture();string owner=f.Business.Owner.OwnerId;
        string other=new OwnerAccess(f.Business.Store).Owners().Single(o=>o.Id!=owner).Id;
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=owner});
        f.Post(f.Draft(FinancialKind.Contribution,10000));f.Post(f.Draft(FinancialKind.Contribution,5000) with{EconomicOwnerId=other});
        f.Post(f.Draft(FinancialKind.Withdrawal,2000));f.Post(f.Draft(FinancialKind.AssetContribution,3000));
        f.Post(f.Draft(FinancialKind.OwnerPaidExpense,4000));f.Post(f.Draft(FinancialKind.OwnerReimbursement,1500));
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        f.Post(f.Draft(FinancialKind.LoanPayment,2600) with{PrincipalCents=2000,InterestCents=500,FeeCents=100});
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),new(2027,1,31)).Owners!;
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Contribution,1000));f.Business.Owner.Lock();
        var actual=source.Read(db=>OwnerSchedule.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,1,31),DebtSchedule.Read(db,source.Source.BusinessId,new(2027,1,1),new(2027,1,31))));
        Assert.Equal(expected.Positions,actual.Positions);Assert.Equal(expected.Accounts,actual.Accounts);
        Assert.Equal(expected.Entries,actual.Entries);Assert.Equal(expected.LoanBalances,actual.LoanBalances);
        var row=actual.Positions.Single(p=>p.OwnerId==owner);
        Assert.Equal(13000,row.ContributionsCents);Assert.Equal(2000,row.DrawsCents);Assert.Equal(1500,row.ReimbursementsCents);
        Assert.Equal(8000,row.AttributedLoanPrincipalCents);Assert.Equal(5000,actual.Positions.Single(p=>p.OwnerId==other).ContributionsCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void MissingLegacyLenderStaysUnattributedAndForeignOwnerScopeHasNoRows()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000));
        f.Business.Store.Write(f.Business.Owner,"Fictional legacy owner report fixture",draft.Id,db=>PostingWriter.Save(db,f.Business.Owner,draft with{LoanLender=null},PostingRules.Build(draft,f.Accounts.Values.ToList()),null));
        f.Post(f.Draft(FinancialKind.LoanPayment,3000) with{PrincipalCents=3000});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        source.Read(db=>
        {
            OwnerScheduleSnapshot Read(string business)=>OwnerSchedule.Read(db,business,f.Date,f.Date,DebtSchedule.Read(db,business,f.Date,f.Date));
            var actual=Read(source.Source.BusinessId);var loan=Assert.Single(actual.LoanBalances);
            Assert.Equal("Historical lender attribution unconfirmed",loan.Attribution);Assert.Empty(loan.OwnerId);Assert.Equal(7000,loan.PrincipalCents);
            Assert.Equal(0,actual.Positions.Single(p=>p.OwnerId==f.Business.Owner.OwnerId).AttributedLoanPrincipalCents);
            var other=Read(foreign.Owner.BusinessId);Assert.Empty(other.Positions);Assert.Empty(other.Entries);Assert.Empty(other.LoanBalances);
            return true;
        });
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }
}
