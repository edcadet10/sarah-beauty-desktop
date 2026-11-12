using SarahBeauty.Business.Setup;

namespace SarahBeauty.Storage.Tests;

public sealed class LiveSetupPolicyTests
{
    internal static BusinessProfile CreationProfile()=>new(){DisplayName="Fictional new LIVE business",Currency="USD",TimeZoneId="UTC",BooksStartDate=new(2027,1,1),NewBusinessOnlyConfirmed=true};
    internal static BusinessProfile ReviewedProfile()=>CreationProfile() with{LegalName="Fictional reviewed legal identity",Formation=new(BusinessSetupState.Confirmed,"Fictional formation review"),EinReview=new(BusinessSetupState.NotApplicable,"Fictional explicit identifier review"),Banking=new(BusinessSetupState.NotApplicable,"Fictional reviewed cash-only scope"),Tax=new(BusinessSetupState.NotApplicable,"Fictional explicitly reviewed tax policy")};
    [Fact]
    public void WorkspaceCreationKeepsUnknownReviewsOwnershipAndOpeningFactsUnknown()
    {
        var value=LiveSetupPolicy.ForCreation(CreationProfile());Assert.Equal("",value.LegalName);Assert.Equal("",value.Ein);Assert.Null(value.ReportingYearStart);Assert.All(new[]{value.Formation,value.EinReview,value.Banking,value.Tax},r=>Assert.Equal(BusinessSetupState.Unconfirmed,r.State));Assert.Equal(5,LiveSetupPolicy.PendingOperationalReviews(value).Count);
    }
    [Theory]
    [InlineData("currency")][InlineData("zone")][InlineData("start")][InlineData("scope")]
    public void CreationRequiresExplicitMinimumBookScope(string missing)
    {
        var profile=CreationProfile();profile=missing switch{"currency"=>profile with{Currency=null},"zone"=>profile with{TimeZoneId=null},"start"=>profile with{BooksStartDate=null},_=>profile with{NewBusinessOnlyConfirmed=false}};Assert.Throws<ArgumentException>(()=>LiveSetupPolicy.ForCreation(profile));
    }
    [Theory]
    [InlineData("legal")][InlineData("formation")][InlineData("ein")][InlineData("banking")][InlineData("tax")]
    public void UnconfirmedOrPendingReviewsCannotBeTreatedAsOperationalReadiness(string missing)
    {
        var profile=ReviewedProfile();profile=missing switch{"legal"=>profile with{LegalName=""},"formation"=>profile with{Formation=new()},"ein"=>profile with{EinReview=new(BusinessSetupState.InProgress,"Fictional review pending")},"banking"=>profile with{Banking=new()},_=>profile with{Tax=new(BusinessSetupState.InProgress,"Fictional review pending")}};Assert.NotEmpty(LiveSetupPolicy.PendingOperationalReviews(profile));Assert.Throws<ArgumentException>(()=>LiveSetupPolicy.RequireOperation(profile,new(2027,1,1),new(2027,1,2,12,0,0,TimeSpan.Zero)));
    }
    [Theory]
    [InlineData(2026,12,31)][InlineData(2027,1,3)]
    public void OperationDateCannotPrecedeBooksOrClaimAFutureActualOperation(int year,int month,int day)=>Assert.Throws<ArgumentException>(()=>LiveSetupPolicy.RequireOperation(ReviewedProfile(),new(year,month,day),new(2027,1,2,12,0,0,TimeSpan.Zero)));
    [Fact]
    public void BusinessTimeZoneDefinesTodayWithoutChangingTheSystemClock()
    {
        var now=new DateTimeOffset(2027,1,2,1,0,0,TimeSpan.Zero);LiveSetupPolicy.RequireOperation(ReviewedProfile(),new(2027,1,2),now);var eastern=ReviewedProfile() with{TimeZoneId="Eastern Standard Time"};Assert.Throws<ArgumentException>(()=>LiveSetupPolicy.RequireOperation(eastern,new(2027,1,2),now));LiveSetupPolicy.RequireOperation(eastern,new(2027,1,1),now);Assert.Empty(LiveSetupPolicy.PendingOperationalReviews(eastern));
    }
}
