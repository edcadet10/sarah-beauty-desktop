using System.Text;
using System.Text.RegularExpressions;
using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Setup;

public static class BusinessProfileRules
{
    public static BusinessProfile Normalize(BusinessProfile draft)
    {
        ArgumentNullException.ThrowIfNull(draft);var value=draft with
        {
            DisplayName=Text(draft.DisplayName,"business display name",200),LegalName=Text(draft.LegalName,"legal name",200),
            ContactEmail=Text(draft.ContactEmail,"business email",254),ContactPhone=Text(draft.ContactPhone,"business phone",100),Address=Address(draft.Address),
            Ein=Text(draft.Ein,"EIN",10),Currency=Optional(draft.Currency,"currency",3),TimeZoneId=Optional(draft.TimeZoneId,"time zone",200),
            Formation=Review(draft.Formation),EinReview=Review(draft.EinReview),Banking=Review(draft.Banking),Tax=Review(draft.Tax)
        };
        if(value.DisplayName.Length==0)throw new ArgumentException("Enter a business display name.");ContactRules.Validate(value.ContactPhone,value.ContactEmail);ValidateIdentity(value);return value;
    }
    private static void ValidateIdentity(BusinessProfile value)
    {
        if(value.Currency is not(null or "USD"))throw new ArgumentException("This app supports USD books. Leave the business currency unconfirmed until it is reviewed.");
        if(value.TimeZoneId is{} zone&&!TimeZoneInfo.TryFindSystemTimeZoneById(zone,out _))throw new ArgumentException("Choose a supported business time zone or leave it unconfirmed.");
        if(value.BooksStartDate==default(DateOnly)||value.ReportingYearStart==default(DateOnly))throw new ArgumentException("Choose a valid business/reporting start date or leave it unknown.");
        if(value.Ein.Length>0&&!Regex.IsMatch(value.Ein,"^[0-9]{2}-[0-9]{7}$",RegexOptions.CultureInvariant))throw new ArgumentException("Use the EIN format 12-3456789, or leave it unknown. Format checking does not verify an EIN.");
        if(value.EinReview.State==BusinessSetupState.Confirmed&&value.Ein.Length==0||value.EinReview.State==BusinessSetupState.NotApplicable&&value.Ein.Length>0)throw new ArgumentException("Match the EIN value to its explicit review state.");
    }
    private static BusinessAddress Address(BusinessAddress value)
    {
        ArgumentNullException.ThrowIfNull(value);return new(Text(value.Line1,"address line 1",200),Text(value.Line2,"address line 2",200),Text(value.City,"city",100),Text(value.Region,"state or region",100),Text(value.PostalCode,"postal code",40),Text(value.Country,"country",100));
    }
    private static BusinessSetupReview Review(BusinessSetupReview value)
    {
        ArgumentNullException.ThrowIfNull(value);string reason=Text(value.Reason,"setup review reason",1000,true);
        if(!Enum.IsDefined(value.State)||value.State!=BusinessSetupState.Unconfirmed&&reason.Length==0)throw new ArgumentException("Record a private reason for each explicit setup review state.");
        return value with{Reason=reason};
    }
    private static string? Optional(string? value,string label,int max){string text=Text(value??"",label,max);return text.Length==0?null:text;}
    private static string Text(string value,string label,int max,bool multiline=false)
    {
        ArgumentNullException.ThrowIfNull(value);string text=value.Trim();
        if(text.Length>max||text.Any(c=>char.IsControl(c)&&!(multiline&&c is '\r' or '\n' or '\t')))throw new ArgumentException("Shorten or remove unsupported control characters from the "+label+".");
        try{new UTF8Encoding(false,true).GetByteCount(text);}catch(EncoderFallbackException){throw new ArgumentException("Use valid Unicode text for the "+label+".");}return text;
    }
}
