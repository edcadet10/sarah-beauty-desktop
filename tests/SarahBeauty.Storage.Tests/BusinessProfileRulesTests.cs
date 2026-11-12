using SarahBeauty.Business.Setup;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessProfileRulesTests
{
    [Fact]
    public void AnIncompleteProfileKeepsUnknownFactsWithoutInventingAStartCurrencyOrReview()
    {
        var value=BusinessProfileRules.Normalize(new(){DisplayName=" Fictional new business "});Assert.Equal("Fictional new business",value.DisplayName);Assert.Equal("",value.LegalName);Assert.Equal("",value.Ein);Assert.Null(value.Currency);Assert.Null(value.TimeZoneId);Assert.Null(value.BooksStartDate);Assert.Null(value.ReportingYearStart);Assert.False(value.NewBusinessOnlyConfirmed);Assert.All(new[]{value.Formation,value.EinReview,value.Banking,value.Tax},r=>Assert.Equal(BusinessSetupState.Unconfirmed,r.State));
    }
    [Fact]
    public void ExplicitPrivateFactsAreNormalizedWithoutCertifyingTheirLegalOrTaxStatus()
    {
        var value=BusinessProfileRules.Normalize(new(){DisplayName=" Fictional Beauty ",LegalName=" Fictional Beauty Company ",ContactEmail=" owner@example.com ",ContactPhone=" +1 (555) 555-0100 ",Address=new(" 1 Fictional Lane ",City:" Example ",Country:" United States "),Ein=" 00-0000000 ",Currency=" USD ",TimeZoneId=" UTC ",BooksStartDate=new(2027,1,1),ReportingYearStart=new(2027,1,1),NewBusinessOnlyConfirmed=true,EinReview=new(BusinessSetupState.Confirmed," Fictional owner review "),Tax=new(BusinessSetupState.InProgress," Fictional policy review pending ")});
        Assert.Equal("Fictional Beauty Company",value.LegalName);Assert.Equal("owner@example.com",value.ContactEmail);Assert.Equal("1 Fictional Lane",value.Address.Line1);Assert.Equal("United States",value.Address.Country);Assert.Equal("USD",value.Currency);Assert.Equal("00-0000000",value.Ein);Assert.Equal("Fictional owner review",value.EinReview.Reason);Assert.Equal(BusinessSetupState.InProgress,value.Tax.State);
    }
    [Theory]
    [InlineData("currency")][InlineData("zone")][InlineData("ein")][InlineData("confirmed ein")][InlineData("not applicable ein")][InlineData("state")][InlineData("reason")][InlineData("email")][InlineData("phone")][InlineData("unicode")][InlineData("control")][InlineData("date")]
    public void InvalidOrContradictoryProfileFactsAreRejected(string defect)
    {
        var draft=new BusinessProfile{DisplayName="Fictional Beauty"};draft=defect switch
        {
            "currency"=>draft with{Currency="CAD"},"zone"=>draft with{TimeZoneId="Fictional/unsupported-zone"},"ein"=>draft with{Ein="12-345678"},"confirmed ein"=>draft with{EinReview=new(BusinessSetupState.Confirmed,"Fictional review")},"not applicable ein"=>draft with{Ein="00-0000000",EinReview=new(BusinessSetupState.NotApplicable,"Fictional review")},"state"=>draft with{Formation=new((BusinessSetupState)99,"Fictional review")},"reason"=>draft with{Banking=new(BusinessSetupState.Confirmed)},"email"=>draft with{ContactEmail="invalid"},"phone"=>draft with{ContactPhone="call later"},"unicode"=>draft with{LegalName="Broken \uD800"},"control"=>draft with{LegalName="Fictional\nname"},_=>draft with{BooksStartDate=default(DateOnly)}
        };Assert.Throws<ArgumentException>(()=>BusinessProfileRules.Normalize(draft));
    }
}
