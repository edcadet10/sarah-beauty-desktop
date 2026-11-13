using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class OwnerSecurityView(BusinessStore store,OwnerSession owner,Action changed)
{
    private readonly PasswordBox _current=Form.Secret("Current sign-in passphrase"),_next=Form.Secret("New sign-in passphrase"),_repeat=Form.Secret("Repeat new sign-in passphrase");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _fields=new();
    private CancellationTokenSource? _cancellation;
    private ContentDialog? _dialog;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(_current,_next,_repeat,Form.Action("Change my sign-in passphrase",_status,Change));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Title("Owner security"),Form.Text("Change the passphrase for "+owner.OwnerName+". Use easy-to-type words, with 15 to 256 characters. Enter your current phrase and repeat the new phrase exactly."),
            Form.Text("After saving, sign in again with your new phrase. Earlier sessions for your account will lose access. Passphrase fields clear after an attempt or when you leave this page."),
            _fields,Form.Action("Cancel passphrase change",_status,()=>{_dialog?.Hide();_cancellation?.Cancel();Clear();return Task.CompletedTask;}),_status,
            Form.Text("If you cannot sign in, review an encrypted recovery archive from the sign-in screen. Preparing a recovered copy requires your portable recovery material and creates a new local instance."));
        panel.Unloaded+=(_,_)=>Leave();return panel;
    }
    internal void Leave(){Clear();_dialog?.Hide();_cancellation?.Cancel();}
    private async Task Change()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current passphrase operation.");
        var values=(Current:_current.Password,Next:_next.Password,Repeated:_repeat.Password);Clear();_cancellation=new();_fields.IsEnabled=false;
        try
        {
            if(values.Next.Length is <15 or >256)throw new ArgumentException("Use a new word-based passphrase with 15 to 256 characters.");
            if(!string.Equals(values.Next,values.Repeated,StringComparison.Ordinal))throw new ArgumentException("The new passphrases do not match. Enter them again.");
            _dialog=new(){XamlRoot=_fields.XamlRoot,Title="Change your sign-in passphrase?",Content="Confirm the change for "+owner.OwnerName+". You will need to sign in again with your new phrase.",PrimaryButtonText="Change passphrase",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close};
            if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Passphrase change cancelled. Enter the phrases again when ready.";return;}
            await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>{new OwnerAccess(store).ChangePassphrase(owner,values.Current,values.Next,values.Repeated,token);return true;},token),_cancellation.Token);
            changed();
        }
        catch(OperationCanceledException){_status.Text="Passphrase operation cancelled. Sign in again before retrying if access has ended.";}
        finally{_dialog=null;Clear();_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
    private void Clear()=>_current.Password=_next.Password=_repeat.Password="";
}
