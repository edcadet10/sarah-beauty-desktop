using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal static class BusinessProfileSummary
{
    internal static string Describe(BusinessProfile value)=>$"Display name: {value.DisplayName}\nLegal name: {Known(value.LegalName)}\nContact email: {Known(value.ContactEmail)} • Phone: {Known(value.ContactPhone)}\nAddress: {Known(string.Join(", ",new[]{value.Address.Line1,value.Address.Line2,value.Address.City,value.Address.Region,value.Address.PostalCode,value.Address.Country}.Where(s=>s.Length>0)))}\nCurrency: {value.Currency??"Unconfirmed"} • Time zone: {value.TimeZoneId??"Unconfirmed"}\nBooks start: {Date(value.BooksStartDate)} • Reporting year start: {Date(value.ReportingYearStart)}\nPrivate EIN: {(value.Ein.Length==0?"Not supplied":"Supplied; not displayed in this summary")}\nNew-business-only acknowledgment: {(value.NewBusinessOnlyConfirmed?"Confirmed":"Unconfirmed")}\n"
        +Review("Formation",value.Formation)+Review("EIN",value.EinReview)+Review("Banking",value.Banking)+Review("Tax",value.Tax);
    private static string Review(string label,BusinessSetupReview value)=>label+": "+BusinessSetupReviewFields.Label(value.State)+" • Reason: "+Known(value.Reason)+" • Supporting file: "+(value.Evidence is{} file?file.Name+" v"+file.Number:"None")+"\n";
    private static string Known(string value)=>value.Length==0?"Unknown":value;
    private static string Date(DateOnly? value)=>value?.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)??"Unknown";
}
