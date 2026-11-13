using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ClientDraftDiscardView
{
    private async Task Confirm(CancellationToken cancellation)
    {
        var choice=_choices.SingleOrDefault(v=>v.Draft.Id==_drafts.SelectedId)??throw new ArgumentException("Choose a saved unissued client draft.");var draft=ClientDraftDiscardRules.Normalize(new(choice.Kind,choice.Draft.Id,choice.Draft.Revision,choice.Hash,_reason.Text));
        if(_retryDraft!=draft){var candidate=draft;draft=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new ClientDraftDiscards(store).Review(owner,choice.Kind,choice.Draft.Id,candidate.Reason,token),token),cancellation);}
        cancellation.ThrowIfCancellationRequested();if(draft.ExpectedRevision!=choice.Draft.Revision||draft.DraftSnapshotHash!=choice.Hash)throw new ArgumentException("The saved draft changed. Refresh saved drafts and review its current contents.");
        if(_retryDraft!=draft){_retryDraft=draft;_request=Guid.NewGuid().ToString();}
        var review=Form.Panel();_phrase=Form.Secret("Confirm "+owner.OwnerName+"'s passphrase for discarding this draft");review.Add(Form.Text(choice.Summary+"\nPrivate reason: "+draft.Reason+"\nThis saved draft becomes unavailable for editing or issuance. Its original contents and this reason are retained permanently. Issued documents and money remain unchanged."),_phrase);
        _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Discard this unissued client draft?",Content=new ScrollViewer{Content=review,MaxHeight=500},PrimaryButtonText="Discard client draft",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Client draft not discarded. Your selection and private reason are preserved.";return;}
        cancellation.ThrowIfCancellationRequested();string phrase=_phrase.Password;_phrase.Password="";string request=_request;
        var saved=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new ClientDraftDiscards(store).Discard(owner,draft,phrase,request,token),token),cancellation);
        cancellation.ThrowIfCancellationRequested();await Load(cancellation);_reason.Text="";_retryDraft=null;_request="";_status.Text="Discarded "+ClientDraftDiscardChoice.KindName(saved.Kind)+" draft. Original contents and private reason are retained; issued documents and money are unchanged.";
    }
}
