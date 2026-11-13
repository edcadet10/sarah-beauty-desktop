using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BusinessProfileView
{
    private async Task Confirm(CancellationToken cancellation)
    {
        var draft=BusinessProfileRules.NormalizeChange(new(_draftRevision,_fields.Read(),_reason.Text));if(_retryDraft!=draft){_retryDraft=draft;_request=Guid.NewGuid().ToString();}
        var review=Form.Panel();_phrase=Form.Secret("Confirm "+owner.OwnerName+"'s passphrase for the business profile");review.Add(Form.Text(BusinessProfileSummary.Describe(draft.Profile)+"\nPrivate change reason: "+draft.Reason+"\nThe original issued documents and financial entries stay unchanged."),_phrase);
        _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Confirm this private business profile?",Content=new ScrollViewer{Content=review,MaxHeight=480,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},PrimaryButtonText="Confirm profile",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Profile not confirmed. Your draft is preserved.";return;}cancellation.ThrowIfCancellationRequested();string phrase=_phrase.Password;_phrase.Password="";string request=_request;
        var saved=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new BusinessProfiles(store).Confirm(owner,draft,phrase,request,token),token),cancellation);
        cancellation.ThrowIfCancellationRequested();await Load(cancellation,true);_status.Text="Confirmed private business-profile revision "+saved.Revision+". Setup states remain as explicitly reviewed; no opening entry was posted.";
    }
    private void ShowHistory()=>_history.Content=PagedReport.Build("Private business-profile history",["Revision","Display name at review","Formation","EIN review","Banking","Tax","Private change reason","Recorded by owner","Recorded at UTC"],
        _versions.OrderByDescending(v=>v.Revision).Select(v=>new[]{v.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),v.Profile.DisplayName,BusinessSetupReviewFields.Label(v.Profile.Formation.State),BusinessSetupReviewFields.Label(v.Profile.EinReview.State),BusinessSetupReviewFields.Label(v.Profile.Banking.State),BusinessSetupReviewFields.Label(v.Profile.Tax.State),v.ChangeReason,v.RecordedByOwnerId,v.RecordedAtUtc}));
}
