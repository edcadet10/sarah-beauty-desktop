using SarahBeauty.Business.Banking;

namespace SarahBeauty.Storage.Tests;

public sealed class StatementErrorCalculationTests
{
    private static readonly DateOnly January=new(2027,1,10);
    private static ReconciliationLine Line(string id,long cents,DateOnly? date=null)=>new(id,date??January,cents,Math.Abs(cents),"Fictional bank entry");
    private static StatementError Error(string bank,long cents)=>new(Guid.NewGuid().ToString(),bank,cents,"Fictional documented bank error","Fictional bank confirmation");
    private static StatementErrorResolution Resolution(string error,string bank,long cents)=>new(error,bank,cents,"Fictional actual bank correction","Fictional correction evidence");

    [Theory]
    [InlineData(2500,-2500)]
    [InlineData(-2500,2500)]
    public void StatementErrorsAdjustTheBankSideInEitherDirection(long amount,long adjustment)
    {
        var error=Error("bank",Math.Abs(amount));
        var result=StatementErrorCalculator.Calculate([Line("bank",amount)],[],[error],[]);
        Assert.Equal(adjustment,result.AdjustmentCents);
        Assert.Equal(Math.Abs(amount),result.ClaimedBankCents["bank"]);
        Assert.Equal(amount,result.RemainingErrors.Single().SignedCents);
    }

    [Fact]
    public void APartialBankCorrectionCarriesOnlyTheUnresolvedAmountIntoLaterStatements()
    {
        var error=Error("original",2500);
        var january=StatementErrorCalculator.Calculate([Line("original",2500)],[],[error],[]);
        var february=StatementErrorCalculator.Calculate([Line("february",-1000,January.AddMonths(1))],january.RemainingErrors,[],[Resolution(error.Id,"february",1000)]);
        Assert.Equal(-1500,february.AdjustmentCents);
        Assert.Equal(1500,february.RemainingErrors.Single().RemainingCents);
        var march=StatementErrorCalculator.Calculate([Line("march",-1500,January.AddMonths(2))],february.RemainingErrors,[],[Resolution(error.Id,"march",1500)]);
        Assert.Equal(0,march.AdjustmentCents);
        Assert.Equal(0,march.RemainingErrors.Single().RemainingCents);
        Assert.Equal(2500,january.RemainingErrors.Single().RemainingCents);
    }

    [Fact]
    public void ErrorsCorrectedWithinTheSameStatementRetainBothSourceClaims()
    {
        var error=Error("wrong",2500);
        var result=StatementErrorCalculator.Calculate([Line("wrong",2500),Line("fix",-2500)],[],[error],[Resolution(error.Id,"fix",2500)]);
        Assert.Equal(0,result.AdjustmentCents);
        Assert.Equal(2,result.ClaimedBankCents.Count);
        Assert.Equal(0,result.RemainingErrors.Single().RemainingCents);
    }

    [Fact]
    public void CombinedResolutionsCannotExceedEitherTheOriginalErrorOrTheBankCorrection()
    {
        var a=Error("a",2500);var b=Error("b",2500);
        var roots=StatementErrorCalculator.Calculate([Line("a",2500),Line("b",2500)],[],[a,b],[]).RemainingErrors;
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("fix",-3000)],roots,[],[Resolution(a.Id,"fix",2000),Resolution(b.Id,"fix",2000)]));
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("one",-2000),Line("two",-2000)],roots,[],[Resolution(a.Id,"one",2000),Resolution(a.Id,"two",1000)]));
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("fix",-3000)],roots,[],[Resolution(a.Id,"fix",1000),Resolution(a.Id,"fix",1000)]));
    }

    [Fact]
    public void ClaimsRequireEvidenceAvailableRemaindersAndDistinctStableIdentities()
    {
        var error=Error("bank",2500);
        foreach(var invalid in new[]{error with{EvidenceId=""},error with{Explanation=" "},error with{Cents=0},error with{Cents=2499},error with{BankLineId="foreign"},error with{Id="unstable"}})
            Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("bank",2500)],[],[invalid],[]));
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("bank",2500)],[],[error,error with{Id=Guid.NewGuid().ToString()}],[]));
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("bank",2500) with{RemainingCents=1000}],[],[error],[]));
    }

    [Fact]
    public void AResolutionCannotInventAnErrorChangeItsDirectionOrPrecedeIt()
    {
        var error=Error("original",2500);
        var roots=StatementErrorCalculator.Calculate([Line("original",2500)],[],[error],[]).RemainingErrors;
        foreach(var invalid in new[]{Resolution("foreign","fix",1000),Resolution(error.Id,"fix",1000) with{EvidenceId=""},Resolution(error.Id,"fix",0)})
            Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("fix",-1000)],roots,[],[invalid]));
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("fix",1000)],roots,[],[Resolution(error.Id,"fix",1000)]));
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("fix",-1000,January.AddDays(-1))],roots,[],[Resolution(error.Id,"fix",1000)]));
    }

    [Fact]
    public void AggregateAmountsRejectOverflowButAllowExactlyOffsettingMaximumValues()
    {
        var a=Error("a",long.MaxValue);var b=Error("b",long.MaxValue);
        Assert.Throws<ArgumentException>(()=>StatementErrorCalculator.Calculate([Line("a",long.MaxValue),Line("b",long.MaxValue)],[],[a,b],[]));
        var result=StatementErrorCalculator.Calculate([Line("a",long.MaxValue),Line("b",-long.MaxValue)],[],[a,b],[]);
        Assert.Equal(0,result.AdjustmentCents);
        Assert.Equal(2,result.RemainingErrors.Count(e=>e.RemainingCents==long.MaxValue));
    }
}
