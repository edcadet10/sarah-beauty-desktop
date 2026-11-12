using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal sealed class BusinessProfileFields
{
    private readonly TextBox _display=Form.Input("Business display name"),_legal=Form.Input("Private legal business name (optional)"),_email=Form.Input("Business contact email (optional)"),_phone=Form.Input("Business contact phone (optional)");
    private readonly TextBox _line1=Form.Input("Business address line 1 (optional)"),_line2=Form.Input("Business address line 2 (optional)"),_city=Form.Input("Business address city (optional)"),_region=Form.Input("Business address state or region (optional)"),_postal=Form.Input("Business address postal code (optional)"),_country=Form.Input("Business address country (optional)");
    private readonly TextBox _ein=Form.Input("Private EIN (optional, 12-3456789)"),_books=Form.Input("Books start date (optional, YYYY-MM-DD)"),_year=Form.Input("Reporting year start (optional, YYYY-MM-DD)");
    private readonly ComboBox _currency=Form.Select("Business currency",["Unconfirmed","USD"],"Unconfirmed");
    private readonly SearchableRecords _zone=new("Business time zone","Unconfirmed");
    private readonly CheckBox _newBusiness=new(){Content=Form.Text("I confirm these records belong only to the new business. Earlier-business records are not automatically imported."),MinHeight=44};
    private readonly BusinessSetupReviewFields _formation=new("Formation"),_einReview=new("EIN"),_banking=new("Banking"),_tax=new("Tax");
    internal UIElement Build()
    {
        _zone.Update(TimeZoneInfo.GetSystemTimeZones().Select(z=>(z.Id,z.DisplayName+" • "+z.Id)));var panel=Form.Panel();panel.Add(_display,_legal,_email,_phone,_line1,_line2,_city,_region,_postal,_country,_ein,_currency,_zone.Build(),_books,_year,_newBusiness,_formation.Build(),_einReview.Build(),_banking.Build(),_tax.Build());return panel;
    }
    internal void Set(BusinessProfile value,IEnumerable<StoredAttachment> files)
    {
        _display.Text=value.DisplayName;_legal.Text=value.LegalName;_email.Text=value.ContactEmail;_phone.Text=value.ContactPhone;_line1.Text=value.Address.Line1;_line2.Text=value.Address.Line2;_city.Text=value.Address.City;_region.Text=value.Address.Region;_postal.Text=value.Address.PostalCode;_country.Text=value.Address.Country;_ein.Text=value.Ein;
        _currency.SelectedItem=value.Currency??"Unconfirmed";_zone.Update(TimeZoneInfo.GetSystemTimeZones().Select(z=>(z.Id,z.DisplayName+" • "+z.Id)),value.TimeZoneId??"");_books.Text=Date(value.BooksStartDate);_year.Text=Date(value.ReportingYearStart);_newBusiness.IsChecked=value.NewBusinessOnlyConfirmed;
        _formation.Set(value.Formation,files);_einReview.Set(value.EinReview,files);_banking.Set(value.Banking,files);_tax.Set(value.Tax,files);
    }
    internal void UpdateFiles(IEnumerable<StoredAttachment> files){_formation.UpdateFiles(files);_einReview.UpdateFiles(files);_banking.UpdateFiles(files);_tax.UpdateFiles(files);}
    internal BusinessProfile Read()=>BusinessProfileRules.Normalize(new BusinessProfile
    {
        DisplayName=_display.Text,LegalName=_legal.Text,ContactEmail=_email.Text,ContactPhone=_phone.Text,Address=new(_line1.Text,_line2.Text,_city.Text,_region.Text,_postal.Text,_country.Text),Ein=_ein.Text,
        Currency=_currency.SelectedItem?.ToString() switch{"Unconfirmed"=>null,"USD"=>"USD",_=>throw new ArgumentException("Choose an explicit business currency state.")},TimeZoneId=_zone.SelectedId.Length==0?null:_zone.SelectedId,
        BooksStartDate=ReadDate(_books.Text),ReportingYearStart=ReadDate(_year.Text),NewBusinessOnlyConfirmed=_newBusiness.IsChecked==true,Formation=_formation.Read(),EinReview=_einReview.Read(),Banking=_banking.Read(),Tax=_tax.Read()
    });
    private static string Date(DateOnly? value)=>value?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"";
    private static DateOnly? ReadDate(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return null;if(DateOnly.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var value))return value;throw new ArgumentException("Use a valid business start date in YYYY-MM-DD format, or leave it unknown.");
    }
}
