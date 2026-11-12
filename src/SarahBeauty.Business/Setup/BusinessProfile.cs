using SarahBeauty.Business.Attachments;

namespace SarahBeauty.Business.Setup;

public enum BusinessSetupState {Unconfirmed,InProgress,Confirmed,NotApplicable}
public sealed record BusinessSetupReview(BusinessSetupState State=BusinessSetupState.Unconfirmed,string Reason="",ManagedEvidenceFile? Evidence=null);
public sealed record BusinessAddress(string Line1="",string Line2="",string City="",string Region="",string PostalCode="",string Country="");
public sealed record BusinessProfile
{
    public string DisplayName {get;init;}="";
    public string LegalName {get;init;}="";
    public string ContactEmail {get;init;}="";
    public string ContactPhone {get;init;}="";
    public BusinessAddress Address {get;init;}=new();
    public string Ein {get;init;}="";
    public string? Currency {get;init;}
    public string? TimeZoneId {get;init;}
    public DateOnly? BooksStartDate {get;init;}
    public DateOnly? ReportingYearStart {get;init;}
    public bool NewBusinessOnlyConfirmed {get;init;}
    public BusinessSetupReview Formation {get;init;}=new();
    public BusinessSetupReview EinReview {get;init;}=new();
    public BusinessSetupReview Banking {get;init;}=new();
    public BusinessSetupReview Tax {get;init;}=new();
}
