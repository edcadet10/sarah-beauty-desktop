using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class RetireStoreView(BusinessStore store,OwnerSession owner,LocalBackupJob job,Action retired)
{
    private readonly TextBox _reason=Form.Input("Local instance retirement reason");
    private readonly PasswordBox _phrase=Form.Secret("Confirm current owner's passphrase to retire this instance");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _fields=new();
    private readonly StackPanel _panel=Form.Panel();
    private CancellationTokenSource? _cancellation;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(Form.Text("Retire this local writable instance before continuing in its recovered copy. A complete unchanged archive must cover its current saved business watermark and instance. Finish or discard unsaved work first. This preserves the local database and archive; it does not retire another computer."),
            _reason,_phrase,Form.Action("Retire local writable instance",_status,Retire));_fields.Content=fields;
        _panel.Add(_fields,Form.Action("Cancel local retirement",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status);_panel.Unloaded+=(_,_)=>{_cancellation?.Cancel();_phrase.Password="";};
        return new Expander{Header="Retire this local writable instance",Content=_panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private async Task Retire()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current local retirement.");string phrase=_phrase.Password,reason=_reason.Text;_cancellation=new();_fields.IsEnabled=false;
        try
        {
            var confirm=new ContentDialog{XamlRoot=_panel.XamlRoot,Title="Retire this local writable instance?",Content="Saved records and the verified archive will be preserved. This instance will reject further business access. Unsaved fields in other sections will be discarded. Activate a prepared recovered copy to continue.",PrimaryButtonText="Retire local instance",CloseButtonText="Keep current instance",DefaultButton=ContentDialogButton.Close};
            if(await confirm.ShowAsync()!=ContentDialogResult.Primary)return;_status.Text="Verifying the current archive and recording local retirement…";
            await Task.Run(()=>new StoreRetirement(store).RetireAsync(owner,job.Id,reason,phrase,_cancellation.Token));retired();
        }
        catch(OperationCanceledException){_status.Text="Local retirement cancelled.";}
        finally{_phrase.Password="";_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
}
