using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;

namespace SarahBeauty.Storage.Tests;

public sealed class SupplierRefundTests
{
    private static FinancialDraft Refund(LedgerFixture f,PostedMovement payment,long cents,string destination="Business cash")=>
        f.Draft(FinancialKind.PurchaseRefund,cents) with{OriginalId=payment.Id,SupplierRefundDestination=destination};

    [Fact]
    public void PartialAndFinalCashRefundsRestoreExactBusinessAndPersonalShares()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        f.Post(Refund(f,payment,17600));
        Assert.Equal(17600,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(550,f.Balance("5000"));
        Assert.Equal(16500,f.Balance("1500"));
        Assert.Equal(550,f.Balance("3100"));
        f.Post(Refund(f,payment,17600));
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance("5000"));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("3100"));
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,payment,1)));
    }

    [Theory]
    [InlineData("Owner contribution","3000")]
    [InlineData("Owner reimbursable","2100")]
    public void BusinessReceivingOwnerFundedRefundRecordsPersonalValueForTheOriginalOwner(string funding,string code)
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200,funding));
        var refund=f.Post(Refund(f,payment,35200));
        Assert.Equal(35200,f.Balance("1010"));
        Assert.Equal(-35200,f.Balance(code));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("5000"));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id});
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(-34100,f.Balance(code));
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
    }

    [Theory]
    [InlineData("Owner contribution","Reverse owner contribution","3000")]
    [InlineData("Owner reimbursable","Reduce owner payable","2100")]
    public void RefundToOriginalOwnerFundingReversesOnlyBusinessValue(string funding,string destination,string code)
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200,funding));
        f.Post(Refund(f,payment,35200,destination));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance(code));
        Assert.Equal(0,f.Balance("5000"));
        Assert.Equal(0,f.Balance("1500"));
    }

    [Fact]
    public void AlreadyReimbursedOwnerCannotReduceThePayableAgainAndCanRecordRetainedRefundAsDraw()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200,"Owner reimbursable"));
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,34100));
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,payment,35200,"Reduce owner payable")));
        f.Post(Refund(f,payment,35200,"Owner withdrawal") with{EconomicOwnerId=f.Business.Owner.OwnerId});
        Assert.Equal(0,f.Balance("2100"));
        Assert.Equal(34100,f.Balance("3100"));
        Assert.Equal(-34100,f.Balance("1010"));
        Assert.Equal(0,f.Balance("5000"));
    }

    [Fact]
    public void RefundFailureRollsBackAndRetryDoesNotDuplicateRestoredValue()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var draft=f.Book.SaveDraft(f.Business.Owner,Refund(f,payment,17600));
        f.Book.FailurePoint=p=>{if(p=="purchase-settlement")throw new IOException("Injected supplier refund failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(0,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(-35200,f.Balance("1010"));
        f.Book.FailurePoint=null;
        var refund=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(refund.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(17600,SupplierPaymentTests.Due(f,bill));
    }

    [Fact]
    public void ActiveRefundBlocksOriginalPaymentReversalUntilTheRefundIsReversed()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var refund=f.Post(Refund(f,payment,17600));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id}));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id});
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(0,f.Balance("1010"));
    }

    [Fact]
    public void LaterRefundReservationPreventsAnEarlierRefundFromExceedingItsOriginalShares()
    {
        using var f=new LedgerFixture();
        var value=PurchaseTests.Draft(f);
        var bill=SupplierPaymentTests.Bill(f,value with{Lines=[value.Lines[0]]});
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,2200));
        f.Post(Refund(f,payment,1500) with{Date=f.Date.AddDays(2)});
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,payment,1500) with{Date=f.Date.AddDays(1)}));
        Assert.Equal(-700,f.Balance("1010"));
        Assert.Equal(350,f.Balance("5000"));
    }

    [Fact]
    public void SignedSettlementMigrationKeepsExistingPaymentAndReversalHistoryBalanced()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,17600));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id});
        f.Business.Files.Execute("DROP INDEX PurchaseSettlements_Refund; ALTER TABLE PurchaseSettlements DROP COLUMN RefundsId; ALTER TABLE PurchaseSettlements DROP COLUMN Sign;");
        f.Business.Files.Execute(PurchaseRefundSchema.Sql);
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SUM(Sign*Cents) FROM PurchaseSettlements;"));
    }
}
