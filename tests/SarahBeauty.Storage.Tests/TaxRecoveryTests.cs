using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class TaxRecoveryTests
{
    private static PostedMovement TaxedReceipt(LedgerFixture f,DateOnly? date=null)=>
        f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,Date=date??f.Date});

    private static FinancialDraft Refund(LedgerFixture f,PostedMovement payment,TaxRefundAllocation? split=null)=>
        f.Draft(FinancialKind.Refund,11000) with{OriginalId=payment.Id,TaxCents=1000,TaxRefundAllocation=split,EvidenceId="Fictional reviewed tax refund treatment"};

    private static PostedMovement Recoverable(LedgerFixture f)
    {
        var payment=TaxedReceipt(f);
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        return f.Post(Refund(f,payment,new(0,1000,0)));
    }

    private static FinancialDraft Recovery(LedgerFixture f,PostedMovement refund,long cents,string destination=TaxRecoveryDestination.Cash)=>
        f.Draft(FinancialKind.TaxRecovery,cents) with{OriginalId=refund.Id,TaxRecoveryDestination=destination,EvidenceId="Fictional actual recovery evidence"};

    private static ReportSnapshot Report(LedgerFixture f)=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,DateOnly.MaxValue);

    [Fact]
    public void AlreadyRemittedTaxRequiresExplicitTreatmentAndActualRecoveryIsNotIncome()
    {
        using var f=new LedgerFixture();
        var payment=TaxedReceipt(f);
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,payment)));
        var refund=f.Post(Refund(f,payment,new(0,1000,0)));
        Assert.Equal(0,f.Balance("2000"));
        Assert.Equal(1000,f.Balance("1250"));
        Assert.Equal(-1000,f.Balance("1010"));
        Assert.Equal(1000,Report(f).Tax.RefundedCents);
        Assert.Equal(1000,Report(f).Tax.RecoverableCents);
        Assert.Equal(0,Report(f).ProfitLoss.IncomeCents);
        var recovery=f.Post(Recovery(f,refund,1000));
        Assert.Equal(0,f.Balance("1250"));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(1000,Report(f).Tax.RecoveredCashCents);
        Assert.Equal(0,Report(f).ProfitLoss.IncomeCents);
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=recovery.Id});
        Assert.Equal(1000,f.Balance("1250"));
        Assert.Equal(0,Report(f).Tax.RecoveredCashCents);
    }

    [Fact]
    public void MixedRefundTreatmentAndDeniedRecoveryKeepActualRefundAndExpenseSeparate()
    {
        using var f=new LedgerFixture();
        var payment=TaxedReceipt(f);
        f.Post(f.Draft(FinancialKind.TaxRemittance,800));
        var refund=f.Post(Refund(f,payment,new(200,300,500)));
        Assert.Equal(0,f.Balance("2000"));
        Assert.Equal(300,f.Balance("1250"));
        Assert.Equal(500,f.Balance("5050"));
        Assert.Equal(1000,Report(f).Tax.RefundedCents);
        var denied=f.Post(Recovery(f,refund,300,TaxRecoveryDestination.WriteOff));
        Assert.Equal(0,f.Balance("1250"));
        Assert.Equal(800,f.Balance("5050"));
        Assert.Equal(800,Report(f).Tax.NonrecoverableCents);
        Assert.Equal(-800,f.Balance("1010"));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=denied.Id});
        Assert.Equal(300,f.Balance("1250"));
        Assert.Equal(500,Report(f).Tax.NonrecoverableCents);
    }

    [Fact]
    public void ActualOffsetNeedsAvailableLiabilityAndCreatesNoCashOrIncome()
    {
        using var f=new LedgerFixture();
        var refund=Recoverable(f);
        Assert.Throws<ArgumentException>(()=>f.Post(Recovery(f,refund,400,TaxRecoveryDestination.Offset)));
        TaxedReceipt(f);
        f.Post(Recovery(f,refund,400,TaxRecoveryDestination.Offset));
        Assert.Equal(-600,f.Balance("2000"));
        Assert.Equal(600,f.Balance("1250"));
        Assert.Equal(10000,f.Balance("1010"));
        Assert.Equal(400,Report(f).Tax.OffsetCents);
        Assert.Equal(0,Report(f).Tax.RecoveredCashCents);
        Assert.Equal(10000,Report(f).ProfitLoss.IncomeCents);
    }

    [Fact]
    public void StaleReviewsAndFutureRecoveryReservationsCannotSpendTheSameClaimTwice()
    {
        using var f=new LedgerFixture();
        var refund=Recoverable(f);
        var first=f.Book.SaveDraft(f.Business.Owner,Recovery(f,refund,600));
        var second=f.Book.SaveDraft(f.Business.Owner,Recovery(f,refund,600));
        f.Book.Approve(f.Business.Owner,first.Id,first.Revision,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,second.Id,second.Revision,BusinessFixture.Phrase));
        Assert.Equal(400,f.Balance("1250"));
        var future=f.Post(Recovery(f,refund,400) with{Date=f.Date.AddDays(2)});
        Assert.Throws<ArgumentException>(()=>f.Post(Recovery(f,refund,1) with{Date=f.Date.AddDays(1)}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id}));
        Assert.Equal(0,f.Balance("1250"));
        Assert.Equal(400,future.Source.AmountCents);
    }

    [Fact]
    public void AnotherRefundCannotFundAnExhaustedTaxClaim()
    {
        using var f=new LedgerFixture();
        var first=Recoverable(f);
        var second=Recoverable(f);
        f.Post(Recovery(f,first,1000));
        Assert.Throws<ArgumentException>(()=>f.Post(Recovery(f,first,1)));
        Assert.Equal(1000,f.Balance("1250"));
        f.Post(Recovery(f,second,1000));
        Assert.Equal(0,f.Balance("1250"));
    }

    [Fact]
    public void ARefundCannotBeUndoneBeforeItsLaterRecoveryAndRecoveryReversal()
    {
        using var f=new LedgerFixture();
        var refund=Recoverable(f);
        var recovery=f.Post(Recovery(f,refund,600) with{Date=f.Date.AddDays(1)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=recovery.Id,Date=f.Date.AddDays(3)});
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id,Date=f.Date.AddDays(2)}));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id,Date=f.Date.AddDays(3)});
        Assert.Equal(0,f.Balance("1250"));
        Assert.Equal(0,Report(f).Tax.RefundedCents);
    }

    [Fact]
    public void InterruptedRecoveryRollsBackAndRetryPostsOnce()
    {
        using var f=new LedgerFixture();
        var refund=Recoverable(f);
        var draft=f.Book.SaveDraft(f.Business.Owner,Recovery(f,refund,500));
        f.Book.FailurePoint=p=>{if(p=="tax-recovery")throw new IOException("Injected tax recovery failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(1000,f.Balance("1250"));
        Assert.Equal(-1000,f.Balance("1010"));
        f.Book.FailurePoint=null;
        var posted=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(posted.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(500,f.Balance("1250"));
    }

    [Fact]
    public void InvalidAllocationUnconfirmedEvidenceAndUnrelatedRecoverySourcesAreRejected()
    {
        using var f=new LedgerFixture();
        var payment=TaxedReceipt(f);
        foreach(var allocation in new[]{new TaxRefundAllocation(-1,1001,0),new(100,100,100),new(1000,1,0)})
            Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,payment,allocation)));
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,payment,new(0,1000,0)) with{EvidenceId=""}));
        Assert.Throws<ArgumentException>(()=>f.Post(Recovery(f,payment,1000)));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Expense,1000) with{TaxRefundAllocation=new(0,0,0)}));
        Assert.Equal(11000,f.Balance("1010"));
    }

    [Fact]
    public void TaxActivityKeepsRemittanceRefundAndRecoveryInTheirOwnPeriods()
    {
        using var f=new LedgerFixture();
        var payment=TaxedReceipt(f);
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000) with{Date=f.Date.AddMonths(1)});
        var refund=f.Post(Refund(f,payment,new(0,1000,0)) with{Date=f.Date.AddMonths(2)});
        var recovered=f.Post(Recovery(f,refund,1000) with{Date=f.Date.AddMonths(3)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=recovered.Id,Date=f.Date.AddMonths(4)});
        var reports=new FinancialReports(f.Business.Store);
        var march=reports.Load(f.Business.Owner,new(2027,3,1),new(2027,3,31));
        Assert.Equal(1000,march.Tax.RefundedCents);
        Assert.Equal(1000,march.Tax.RecoverableCents);
        Assert.Equal(0,march.Tax.LiabilityCents);
        Assert.Equal(0,march.Tax.RecoveredCashCents);
        var april=reports.Load(f.Business.Owner,new(2027,4,1),new(2027,4,30));
        Assert.Equal(1000,april.Tax.RecoveredCashCents);
        Assert.Equal(0,april.Tax.RecoverableCents);
        var may=reports.Load(f.Business.Owner,new(2027,5,1),new(2027,5,31));
        Assert.Equal(-1000,may.Tax.RecoveredCashCents);
        Assert.Equal(1000,may.Tax.RecoverableCents);
    }

    [Fact]
    public void ExistingBusinessGainsRecoveryAccountsWithoutChangingItsPostedHistory()
    {
        using var f=new LedgerFixture();
        TaxedReceipt(f);
        f.Business.Files.Execute("DROP TABLE StatementRows; DROP TABLE BankLines; DROP TABLE StatementImports; DELETE FROM Accounts WHERE Code IN('1250','5050'); PRAGMA user_version=18;");
        var upgraded=LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath);
        Assert.Equal(BusinessSchema.Version,upgraded.SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var chart=new ChartOfAccounts(f.Business.Store).List(f.Business.Owner);
        Assert.Equal("Asset",chart.Single(a=>a.Code=="1250").Type);
        Assert.Equal("Expense",chart.Single(a=>a.Code=="5050").Type);
        Assert.Equal(11000,f.Balance("1010"));
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }
}
