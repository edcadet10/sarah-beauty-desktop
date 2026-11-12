using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BusinessProfileView
{
    private async Task Confirm(CancellationToken cancellation)
    {
        var draft=BusinessProfileRules.NormalizeChange(new(_draftRevision,_fields.Read(),_reason.Text));if(_retryDraft!=draft){_retryDraft=draft;_request=Guid.NewGuid().ToString();}
        var review=Form.Panel();_phrase=Form.Secret("Confirm "+owner.OwnerName+"'s passphrase for the business profile");review.Add(Form.Text(Summary(draft.Profile)+"\nPrivate change reason: "+draft.Reason+"\nThe original issued documents and financial entries stay unchanged."),_phrase);
        _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Confirm this private business profile?",Content=new ScrollViewer{Content=review,MaxHeight=480,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},PrimaryButtonText="Confirm profile",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Profile not confirmed. Your draft is preserved.";return;}cancellation.ThrowIfCancellationRequested();string phrase=_phrase.Password;_phrase.Password="";string request=_request;
        var saved=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new BusinessProfiles(store).Confirm(owner,draft,phrase,request,token),token),cancellation);
        cancellation.ThrowIfCancellationRequested();await Load(cancellation,true);_status.Text="Confirmed private business-profile revision "+saved.Revision+". Setup states remain as explicitly reviewed; no opening entry was posted.";
    }
    private static string Summary(BusinessProfile value)=>$"Display name: {value.DisplayName}\nLegal name: {Known(value.LegalName)}\nContact email: {Known(value.ContactEmail)} • Phone: {Known(value.ContactPhone)}\nAddress: {Known(string.Join(", ",new[]{value.Address.Line1,value.Address.Line2,value.Address.City,value.Address.Region,value.Address.PostalCode,value.Address.Country}.Where(s=>s.Length>0)))}\nCurrency: {value.Currency??"Unconfirmed"} • Time zone: {value.TimeZoneId??"Unconfirmed"}\nBooks start: {Date(value.BooksStartDate)} • Reporting year start: {Date(value.ReportingYearStart)}\nPrivate EIN: {(value.Ein.Length==0?"Not supplied":"Supplied; not displayed in this summary")}\nNew-business-only acknowledgment: {(value.NewBusinessOnlyConfirmed?"Confirmed":"Unconfirmed")}\n"
        +Review("Formation",value.Formation)+Review("EIN",value.EinReview)+Review("Banking",value.Banking)+Review("Tax",value.Tax);
    private static string Review(string label,BusinessSetupReview value)=>label+": "+BusinessSetupReviewFields.Label(value.State)+" • Reason: "+Known(value.Reason)+" • Supporting file: "+(value.Evidence is{} file?file.Name+" v"+file.Number:"None")+"\n";
    private static string Known(string value)=>value.Length==0?"Unknown":value;
    private static string Date(DateOnly? value)=>value?.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)??"Unknown";
    private void ShowHistory()=>_history.Content=PagedReport.Build("Private business-profile history",["Revision","Display name at review","Formation","EIN review","Banking","Tax","Private change reason","Recorded by owner","Recorded at UTC"],
        _versions.OrderByDescending(v=>v.Revision).Select(v=>new[]{v.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),v.Profile.DisplayName,BusinessSetupReviewFields.Label(v.Profile.Formation.State),BusinessSetupReviewFields.Label(v.Profile.EinReview.State),BusinessSetupReviewFields.Label(v.Profile.Banking.State),BusinessSetupReviewFields.Label(v.Profile.Tax.State),v.ChangeReason,v.RecordedByOwnerId,v.RecordedAtUtc}));
}
