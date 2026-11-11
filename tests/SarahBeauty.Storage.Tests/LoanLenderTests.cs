using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class LoanLenderTests
{
    [Fact]
    public void ChangedLenderBeforeApprovalRequiresResavedDraftAndRetainsApprovedIdentity()
    {
        using var f=new LedgerFixture();var resources=new BusinessResources(f.Business.Store);
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000));
        var loan=resources.SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,draft));
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        draft=f.Book.SaveDraft(f.Business.Owner,draft);
        var source=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(new(loan.Id,loan.Name,f.Business.Owner.OwnerId,"Alex"),source.Source.LoanLender);
        resources.SaveLoan(f.Business.Owner,loan with{Name="Fictional updated display name",Terms="Fictional changed current terms"});
        Assert.Equal(loan.Name,f.Book.Posted(f.Business.Owner).Single().Source.LoanLender!.LenderName);
        var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id,Date=f.Date.AddDays(1)});
        Assert.Equal(source.Source.LoanLender,reversal.Source.LoanLender);
        Assert.True(resources.LoanLenderLocked(f.Business.Owner,loan.Id));
        Assert.Equal(0,f.Balance("2300"));
    }

    [Fact]
    public void LoanOpeningAndRepaymentCaptureLenderAndExactApprovalRetryKeepsTheOriginalFacts()
    {
        using var f=new LedgerFixture();var resources=new BusinessResources(f.Business.Store);
        var loan=resources.SaveLoan(f.Business.Owner,f.Loan with{OpeningDate=f.Date,OpeningPrincipalCents=20000,OwnerLenderId=f.Business.Owner.OwnerId});
        var opening=f.Post(f.Draft(FinancialKind.Opening,20000) with{LoanId=loan.Id,CounterAccountId=f.Accounts["2300"].Id,
            OpeningCredit=true,EvidenceId="Fictional reviewed loan opening"});
        Assert.Equal(loan.Id,opening.Source.LoanLender!.LoanId);
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanPayment,6000) with{PrincipalCents=5000,InterestCents=800,FeeCents=200});
        var payment=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        resources.SaveLoan(f.Business.Owner,loan with{Name="Fictional renamed lender"});
        var retry=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(payment.Id,retry.Id);Assert.Equal(payment.Source.LoanLender,retry.Source.LoanLender);
        Assert.Equal(-15000,f.Balance("2300"));Assert.Equal(800,f.Balance("5030"));Assert.Equal(200,f.Balance("5035"));
    }

    [Fact]
    public void ForgedOrOldDraftFactsNeedResavingAndForeignBusinessCannotQueryProtectedLoan()
    {
        using var f=new LedgerFixture();using var other=new BusinessFixture();
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000) with{LoanLender=new("forged","forged","forged","forged")});
        Assert.Equal(f.Loan.Id,draft.LoanLender!.LoanId);Assert.Empty(draft.LoanLender.OwnerLenderId);
        Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,draft with{LoanLender=null}));
        var legacy=JsonSerializer.SerializeToNode(draft)!.AsObject();legacy.Remove("LoanLender");
        Assert.Null(JsonSerializer.Deserialize<FinancialDraft>(legacy.ToJsonString())!.LoanLender);
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.False(new BusinessResources(other.Store).LoanLenderLocked(other.Owner,f.Loan.Id));
        var second=new OwnerAccess(f.Business.Store).Owners().Single(o=>o.Id!=f.Business.Owner.OwnerId);
        Assert.Throws<ArgumentException>(()=>new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=second.Id}));
    }
}
