using SarahBeauty.Business.Ownership;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerInterestRulesTests
{
    private static readonly string First=Guid.NewGuid().ToString(),Second=Guid.NewGuid().ToString();
    [Fact]
    public void ExactFractionsAcceptExplicitZeroAndCopyTheCallersList()
    {
        var inputs=new List<OwnerInterestInput>{new(First,0m),new(Second,1m)};
        var value=OwnerInterestRules.Normalize(new(0,new(2027,1,1),inputs," Fictional confirmation "));
        inputs.Clear();Assert.Equal(2,value.Shares.Count);Assert.Equal(1m,value.Shares.Sum(s=>s.Fraction));Assert.Equal("Fictional confirmation",value.Reason);
        Assert.Throws<NotSupportedException>(()=>((IList<OwnerInterestInput>)value.Shares).Clear());
    }
    [Theory]
    [InlineData(null,"1")][InlineData("-0.1","1.1")][InlineData("0.6","0.3")][InlineData("0.6","0.5")][InlineData("0.0000001","0.9999999")]
    public void MissingNegativeExcessivePrecisionOrUnbalancedFractionsAreRejected(string? first,string second)
    {
        decimal? amount=first is null?null:decimal.Parse(first,System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentException>(()=>OwnerInterestRules.Normalize(new(0,new(2027,1,1),[new(First,amount),new(Second,decimal.Parse(second,System.Globalization.CultureInfo.InvariantCulture))],"Fictional review")));
    }
    [Fact]
    public void DuplicateOwnersEmptyReasonsAndStaleRevisionShapesAreRejected()
    {
        var draft=new OwnerInterestDraft(0,new(2027,1,1),[new(First,.333333m),new(Second,.666667m)],"Fictional review");
        Assert.Equal(1m,OwnerInterestRules.Normalize(draft).Shares.Sum(s=>s.Fraction));
        foreach(var invalid in new[]{draft with{Shares=[new(First,.5m),new(First.ToUpperInvariant(),.5m)]},draft with{Reason=" "},draft with{ExpectedRevision=-1},draft with{ExpectedRevision=long.MaxValue},draft with{EffectiveFrom=default},draft with{SupersedesId="not a version"}})
            Assert.Throws<ArgumentException>(()=>OwnerInterestRules.Normalize(invalid));
    }
}
