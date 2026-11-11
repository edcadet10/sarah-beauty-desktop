using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class TaxPeriodTests
{
    private static TaxPeriod Period()=>new(){Name="Fictional January sales tax",Jurisdiction="Fictional test jurisdiction",From=new(2027,1,1),Through=new(2027,1,31)};
    private static TaxFilingDecision Filed(TaxPeriod period)=>new(Guid.NewGuid().ToString(),period.Revision,"Filed",new(2027,2,15),1000,"Fictional filing reference","Fictional filing evidence","Recorded actual filing");

    [Fact]
    public void RemittanceDoesNotFileTheReturnAndFilingHistoryDoesNotPostMoney()
    {
        using var f=new LedgerFixture();
        var book=new TaxPeriodBook(f.Business.Store);
        var period=book.Save(f.Business.Owner,Period());
        Assert.Null(period.DueDate);
        f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        Assert.Equal("Unreviewed",book.List(f.Business.Owner).Single().Status);
        var decision=Filed(period);
        var filed=book.RecordStatus(f.Business.Owner,period.Id,decision,BusinessFixture.Phrase);
        var retry=book.RecordStatus(f.Business.Owner,period.Id,decision,BusinessFixture.Phrase);
        Assert.Equal(filed.Revision,retry.Revision);
        Assert.Single(retry.History);
        Assert.Equal(f.Business.Owner.OwnerId,retry.History[0].OwnerId);
        Assert.Equal(10000,f.Balance("1010"));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Record tax filing status';"));
        Assert.Equal(0,f.Balance("2000"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
        var amended=book.RecordStatus(f.Business.Owner,period.Id,Filed(filed) with{Status="Amended",ReportedTaxCents=-100},BusinessFixture.Phrase);
        Assert.Equal(2,amended.History.Length);
        Assert.Equal(1000,amended.History[0].Decision.ReportedTaxCents);
        Assert.Equal(-100,amended.History[1].Decision.ReportedTaxCents);
    }

    [Fact]
    public void UnknownAndNotRequiredRemainExplicitAndHistoryCannotBeForged()
    {
        using var f=new LedgerFixture();
        var book=new TaxPeriodBook(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>book.Save(f.Business.Owner,Period() with{Status="Filed"}));
        var period=book.Save(f.Business.Owner,Period());
        var noReturn=new TaxFilingDecision(Guid.NewGuid().ToString(),period.Revision,"Not required",null,null,"","Fictional reviewed policy","Explicit review");
        var reviewed=book.RecordStatus(f.Business.Owner,period.Id,noReturn,BusinessFixture.Phrase);
        Assert.Equal("Not required",reviewed.Status);
        Assert.Throws<ArgumentException>(()=>book.Save(f.Business.Owner,reviewed with{History=[]}));
        Assert.Throws<ArgumentException>(()=>book.RecordStatus(f.Business.Owner,period.Id,noReturn with{Reason="Changed retry payload"},BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.RecordStatus(f.Business.Owner,period.Id,Filed(period),BusinessFixture.Phrase));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void FilingRequiresReviewedDetailsAndPreservesPeriodIdentity()
    {
        using var f=new LedgerFixture();
        var book=new TaxPeriodBook(f.Business.Store);
        var period=book.Save(f.Business.Owner,Period());
        foreach(var invalid in new[]{Filed(period) with{EvidenceId=""},Filed(period) with{FilingDate=null},Filed(period) with{ReportedTaxCents=null},
            Filed(period) with{Reference=""},Filed(period) with{FilingDate=new(2027,1,15)},Filed(period) with{Status="Amended"}})
            Assert.Throws<ArgumentException>(()=>book.RecordStatus(f.Business.Owner,period.Id,invalid,BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Business.Owner,Period() with{Name="Fictional overlap",From=new(2027,1,15),Through=new(2027,2,28)}));
        var filed=book.RecordStatus(f.Business.Owner,period.Id,Filed(period),BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>book.RecordStatus(f.Business.Owner,period.Id,Filed(filed) with{Status="Amended",FilingDate=new(2027,2,1)},BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Business.Owner,filed with{Through=new(2027,2,28)}));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Business.Owner,filed with{Jurisdiction="Different jurisdiction"}));
        Assert.Equal("Filed",book.List(f.Business.Owner).Single().Status);
    }

    [Fact]
    public void FailureRollsBackHistoryAndInvalidPassphraseCannotRecordStatus()
    {
        using var f=new LedgerFixture();
        var book=new TaxPeriodBook(f.Business.Store);
        var period=book.Save(f.Business.Owner,Period());
        var decision=Filed(period);
        Assert.Throws<UnauthorizedAccessException>(()=>book.RecordStatus(f.Business.Owner,period.Id,decision,"Fictional wrong passphrase"));
        book.BeforeCommit=()=>throw new IOException("Injected filing status failure");
        Assert.Throws<IOException>(()=>book.RecordStatus(f.Business.Owner,period.Id,decision,BusinessFixture.Phrase));
        Assert.Empty(book.List(f.Business.Owner).Single().History);
        Assert.Equal("Unreviewed",book.List(f.Business.Owner).Single().Status);
        book.BeforeCommit=null;
        Assert.Single(book.RecordStatus(f.Business.Owner,period.Id,decision,BusinessFixture.Phrase).History);
    }
}
