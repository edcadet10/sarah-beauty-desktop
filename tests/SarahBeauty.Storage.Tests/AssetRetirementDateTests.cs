using System.Globalization;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class AssetRetirementDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ADisposalCannotPrecedeAnAlreadyPostedLaterAcquisition(string culture)
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.AssetPurchase,30000));f.Post(f.Draft(FinancialKind.AssetPurchase,1000) with{Date=f.Date.AddDays(1)});var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.AssetDisposal,30000) with{AssetId=f.Asset.Id,EvidenceId="Fictional reviewed disposal"});long watermark=Watermark(f);WithCulture(culture,()=>Assert.Contains("retirement date precedes",Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal));Assert.Equal(watermark,Watermark(f));Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ADisposalAfterAllExistingAcquisitionsIsAllowed(string culture)
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.AssetPurchase,30000));f.Post(f.Draft(FinancialKind.AssetPurchase,1000) with{Date=f.Date.AddDays(1)});WithCulture(culture,()=>f.Post(f.Draft(FinancialKind.AssetDisposal,31000) with{AssetId=f.Asset.Id,Date=f.Date.AddDays(1),EvidenceId="Fictional reviewed disposal"}));Assert.Equal(f.Date.AddDays(1),new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);Assert.Equal(0,f.Balance("1500"));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ARecoveryReversalOnALaterDateDoesNotPermitAnEarlierRetirementReversal(string culture)
    {
        using var f=new LedgerFixture();string retired=RetiredWithReversedRecovery(f);var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Reversal,0) with{OriginalId=retired,Date=f.Date.AddDays(1)});long watermark=Watermark(f);WithCulture(culture,()=>Assert.Contains("later asset recovery",Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal));Assert.Equal(watermark,Watermark(f));Assert.Equal(retired,new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposalMovementId);
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void RetirementCanReverseOnceTheLaterRecoveryWasReversedByThatDate(string culture)
    {
        using var f=new LedgerFixture();string retired=RetiredWithReversedRecovery(f);WithCulture(culture,()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=retired,Date=f.Date.AddDays(2)}));Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().DisposedDate);Assert.Equal(30000,f.Balance("1500"));Assert.Equal(-10000,f.Balance("1590"));
    }
    private static string RetiredWithReversedRecovery(LedgerFixture f)
    {
        var source=f.Post(f.Draft(FinancialKind.AssetPurchase,30000));f.Post(f.Draft(FinancialKind.Depreciation,10000));FinancialDraft Refund(long cents,string treatment)=>f.Draft(FinancialKind.SupplierRefund,cents) with{OriginalId=source.Id,CounterAccountId=source.Source.CounterAccountId,EvidenceId="Fictional asset refund decision",AssetAdjustments=[new(f.Asset.Id,treatment)]};var returned=f.Post(Refund(20000,AssetValueChoice.Retire));var recovery=f.Post(Refund(10000,AssetValueChoice.Recover) with{Date=f.Date.AddDays(1)});f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=recovery.Id,Date=f.Date.AddDays(2)});return returned.Id;
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ActiveBankMatchesStillProtectTheirMovementFromReversal(string culture)
    {
        using var f=new LedgerFixture();var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var bank=BankMatchingTests.Statement(f,10000).Single();var cash=BankMatchingTests.Cash(f,payment);var matching=new BankMatching(f.Business.Store);BankMatchingTests.Match(f,matching,f.Date,new MatchPortion(bank.Id,cash.Id,10000));var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id});long watermark=Watermark(f);WithCulture(culture,()=>{Assert.Equal(f.Date,Assert.Single(matching.History(f.Business.Owner)).Date);Assert.Equal(f.Date,Assert.Single(matching.CashLines(f.Business.Owner,cash.AccountId)).Date);Assert.Contains("Unmatch",Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal);});Assert.Equal(watermark,Watermark(f));Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Single(matching.History(f.Business.Owner));
    }
    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static void WithCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}finally{CultureInfo.CurrentCulture=previous;}
    }
}
