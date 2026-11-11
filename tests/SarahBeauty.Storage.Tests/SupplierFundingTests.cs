using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;

namespace SarahBeauty.Storage.Tests;

public sealed class SupplierFundingTests
{
    [Fact]
    public void EverySmallSplitKeepsBusinessDrawAndOutsidePersonalPartsWithinTheirOriginalValue()
    {
        for(long gross=1;gross<=12;gross++)
        for(long business=0;business<=gross;business++)
        for(long draw=0;draw<=gross-business;draw++)
        for(long take=1;take<=gross;take++)
        {
            var original=new SupplierFundingShare(gross,business,draw);
            var part=original.Portion(take);
            Assert.Equal(take,part.Gross);
            Assert.InRange(part.Business,0,business);
            Assert.InRange(part.Draw,0,draw);
            Assert.InRange(part.OutsidePersonal,0,original.OutsidePersonal);
            var remainder=new SupplierFundingShare(gross-take,business-part.Business,draw-part.Draw);
            Assert.Equal(original.OutsidePersonal-part.OutsidePersonal,remainder.OutsidePersonal);
            Assert.Equal(remainder,remainder.Portion(remainder.Gross));
        }
    }

    [Fact]
    public void FundingSplitSupportsTheMaximumGrossWithoutOverflowOrAnExtraCent()
    {
        var original=new SupplierFundingShare(long.MaxValue,long.MaxValue/3,long.MaxValue/3);
        var part=original.Portion(long.MaxValue/2);
        Assert.Equal(part.Gross,checked(part.Business+part.Draw+part.OutsidePersonal));
        Assert.InRange(part.Business,0,original.Business);
        Assert.InRange(part.Draw,0,original.Draw);
        Assert.InRange(part.OutsidePersonal,0,original.OutsidePersonal);
    }

    [Fact]
    public void FundingMigrationRetainsDrawSharesAcrossActualRefundAndReversalHistory()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var refund=f.Post(f.Draft(FinancialKind.PurchaseRefund,17600) with{OriginalId=payment.Id});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id});
        f.Business.Files.Execute("DROP INDEX PurchaseSettlements_Consumes; ALTER TABLE PurchaseSettlements DROP COLUMN ConsumesId; ALTER TABLE PurchaseSettlements DROP COLUMN DrawCents;");
        f.Business.Files.Execute(SupplierFundingSchema.Sql);
        Assert.Equal(35200,SupplierPaymentTests.Due(f,bill));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SUM(Sign*DrawCents) FROM PurchaseSettlements;"));
        Assert.Equal(1100L,TestStore.Scalar(f.Business.Files.DatabasePath,$"SELECT SUM(DrawCents) FROM PurchaseSettlements WHERE MovementId='{payment.Id}';"));
        Assert.Equal(0,f.Balance("3100"));
    }
}
