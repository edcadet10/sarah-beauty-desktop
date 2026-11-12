using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class LiveBusinessCreationView
{
    private async Task Review()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current LIVE creation operation.");var draft=Read();_cancellation=new();_controls.IsEnabled=false;
        try
        {
            var pending=LiveSetupPolicy.PendingOperationalReviews(draft.Change.Profile);string summary=BusinessProfileSummary.Describe(draft.Change.Profile)+"\nFirst owner: "+draft.First+"\nSecond owner: "+draft.Second+"\nPrivate creation reason: "+draft.Change.Reason+"\nPending operational reviews: "+(pending.Count==0?"None recorded":string.Join(", ",pending))+"\nOwnership and openings remain unconfirmed. TEST and earlier-business entries are not imported.";
            _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Create this clean LIVE business?",Content=new ScrollViewer{Content=Form.Text(summary),MaxHeight=480,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},PrimaryButtonText="Create LIVE business",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
            if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Draft kept. Enter both owners' passphrases again before creating.";return;}
            var result=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new LiveBusinesses(parent).Create(draft.Change.Profile,draft.First,draft.FirstPhrase,draft.Second,draft.SecondPhrase,draft.Change.Reason,token),token),_cancellation.Token);bool retained=false;
            try{if(!_panel.IsLoaded||_cancellation.IsCancellationRequested)return;created(result);retained=true;}finally{if(!retained)result.Owner.Lock();}
        }
        catch(OperationCanceledException){_status.Text="LIVE creation interrupted. Reopen LIVE to check its saved state before trying again.";}
        finally{Clear();_dialog=null;_controls.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
}
