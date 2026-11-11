using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReceiptSimilarityTests
{
    [Fact]
    public void TwoRealReceiptsWithDistinctEvidenceWarnButCanBothPostExactlyOnce()
    {
        using var f=new LedgerFixture();var book=new ReceiptSimilarities(f.Business.Store);
        var first=f.Post(f.Draft(FinancialKind.Receipt,12000) with{FeeCents=360,EvidenceId="Fictional first actual payment"});
        var second=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Receipt,12000) with{FeeCents=360,EvidenceId="Fictional second actual payment"});
        var warning=book.Find(f.Business.Owner,second);
        Assert.Equal(1,warning.Total);Assert.Equal(first.Id,warning.Receipts.Single().MovementId);
        Assert.Equal("Fictional first actual payment",warning.Receipts.Single().EvidenceId);
        Assert.Equal("Fictional payer",warning.Receipts.Single().PayerName);
        var posted=f.Book.Approve(f.Business.Owner,second.Id,second.Revision,BusinessFixture.Phrase);
        Assert.Equal(posted.Id,f.Book.Approve(f.Business.Owner,second.Id,second.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(23280,f.Balance("1010"));Assert.Equal(-24000,f.Balance("4000"));Assert.Equal(720,f.Balance("5020"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
        Assert.Equal(1,book.Find(f.Business.Owner,posted.Source).Total);
    }

    [Fact]
    public void GrossReceiptsAcrossCashAccountsAreComparedWithoutTreatingExpensesOrOtherDatesAsReceipts()
    {
        using var f=new LedgerFixture();var book=new ReceiptSimilarities(f.Business.Store);
        f.Post(f.Draft(FinancialKind.Receipt,10000) with{CashAccountId=f.Accounts["1020"].Id,FeeCents=300});
        f.Post(f.Draft(FinancialKind.Expense,10000));
        f.Post(f.Draft(FinancialKind.Receipt,10000) with{Date=f.Date.AddDays(1)});
        f.Post(f.Draft(FinancialKind.Receipt,10001));
        var result=book.Find(f.Business.Owner,f.Draft(FinancialKind.Receipt,10000));
        Assert.Equal(1,result.Total);Assert.Equal("Processor clearing",result.Receipts.Single().AccountName);
        Assert.Equal(10000,result.Receipts.Single().GrossCents);
        Assert.Equal(0,book.Find(f.Business.Owner,f.Draft(FinancialKind.Expense,10000)).Total);
        Assert.Equal(0,book.Find(f.Business.Owner,f.Draft(FinancialKind.Receipt,10000) with{Date=null}).Total);
    }

    [Fact]
    public void ClassifyingReceivedValueDoesNotBecomeAnotherCashReceiptCandidate()
    {
        using var f=new LedgerFixture();var book=new ReceiptSimilarities(f.Business.Store);
        var unknown=f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,5000));
        f.Post(f.Draft(FinancialKind.ReceiptClassification,5000) with{OriginalId=unknown.Id,CustomerId=f.CustomerId,CounterAccountId=f.Accounts["4000"].Id,EvidenceId="Fictional reviewed classification"});
        var result=book.Find(f.Business.Owner,f.Draft(FinancialKind.Receipt,5000));
        Assert.Equal(1,result.Total);Assert.Equal(unknown.Id,result.Receipts.Single().MovementId);
        Assert.Equal("Payer unknown",result.Receipts.Single().PayerName);
        Assert.Equal(0,book.Find(f.Business.Owner,f.Draft(FinancialKind.ReceiptClassification,5000)).Total);
    }

    [Fact]
    public void PostingCancellationRemainsVisibleWithoutBeingCountedAsAnotherReceipt()
    {
        using var f=new LedgerFixture();var source=f.Post(f.Draft(FinancialKind.Receipt,5000));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id,Date=f.Date.AddDays(1)});
        var result=new ReceiptSimilarities(f.Business.Store).Find(f.Business.Owner,f.Draft(FinancialKind.Receipt,5000));
        Assert.Equal(1,result.Total);Assert.Equal(f.Date.AddDays(1),result.Receipts.Single().CancelledOn);
    }

    [Fact]
    public void SimilarityDoesNotRelaxReliableReferenceOrBusinessAccessGuards()
    {
        using var f=new LedgerFixture();var value=f.Draft(FinancialKind.Receipt,5000) with{ReliableReference=true,Provider="Fictional provider",ExternalReference="fictional-unique-1"};
        f.Post(value);var draft=f.Book.SaveDraft(f.Business.Owner,value);var book=new ReceiptSimilarities(f.Business.Store);
        Assert.Equal(1,book.Find(f.Business.Owner,draft).Total);
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        using var foreign=new LedgerFixture();
        Assert.Throws<UnauthorizedAccessException>(()=>book.Find(foreign.Business.Owner,value));
    }
}
