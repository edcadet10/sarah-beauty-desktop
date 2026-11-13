using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class DocumentDecisionView
{
    private async Task Confirm(CancellationToken cancellation)
    {
        var draft=Read();if(_retryDraft!=draft){_retryDraft=draft;_request=Guid.NewGuid().ToString();}
        var review=Form.Panel();_phrase=Form.Secret("Confirm "+owner.OwnerName+"'s passphrase for the document decision");
        review.Add(Form.Text($"{_selected}\nAction: {draft.Action}\nEffective date: {Date(draft.DecisionDate)}\nPrivate reason: {draft.Reason}\nSupporting file: {draft.Evidence?.Name??"None"}\nThe original issued document stays unchanged. This decision is retained permanently."),_phrase);
        _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Record this document decision?",Content=review,PrimaryButtonText="Record decision",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Decision not recorded. Your draft is preserved.";return;}
        cancellation.ThrowIfCancellationRequested();string phrase=_phrase.Password;_phrase.Password="";string request=_request;
        var saved=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
        {
            var book=new DocumentDecisions(store);return draft.Action==DocumentDecisionAction.VoidInvoice?book.VoidInvoice(owner,draft,phrase,request,token):book.RecordQuote(owner,draft,phrase,request,token);
        },token),cancellation);
        cancellation.ThrowIfCancellationRequested();await Load(cancellation);_status.Text="Recorded "+saved.Action+" for the exact issued version. Original PDF and financial history are preserved.";
    }
}
