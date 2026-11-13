using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class OwnerPinView(BusinessStore store,OwnerSession owner)
{
    private readonly PasswordBox _password=Form.Secret("Current passphrase for PIN changes"),_pin=PinInput.Create("New PIN"),_repeat=PinInput.Create("Repeat new PIN");
    private readonly TextBlock _status=Form.Text();
    private readonly TextBlock _state=Form.Muted("Checking local PIN status…");
    private readonly StackPanel _fields=Form.Panel();
    private readonly ContentControl _host=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private CancellationTokenSource? _cancellation;
    private bool _loaded;
    private int _statusVersion;

    internal UIElement Build()
    {
        _fields.Add(Form.Text("Set or replace your PIN"),Form.Text("Choose 6 to 12 digits for "+owner.OwnerName+". This optional PIN works for your local business in this Windows profile. Your password remains available."),
            _state,_password,_pin,_repeat,Form.PrimaryAction("Save my PIN",_status,()=>Run(false)),Form.Action("Remove my PIN",_status,()=>Run(true)),_status,
            Form.Text("Use your current password to change the PIN. After changing your password or restoring your business, sign in with your password and set a PIN again. Approval confirmations still use your password."));
        _fields.Loaded+=async(_,_)=>{_loaded=true;await ShowState(++_statusVersion);};_fields.Unloaded+=(_,_)=>Leave();_host.Content=_fields;return Form.Card(_host);
    }
    internal void Leave(){_loaded=false;_statusVersion++;Clear();_cancellation?.Cancel();}
    private async Task ShowState(int version)
    {
        try
        {
            bool available=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new OwnerPins(store).IsConfigured(owner.OwnerId),token));
            if(_loaded&&version==_statusVersion)_state.Text=available?"PIN is enabled for this owner in this Windows profile.":"PIN is not available here. Use your password or set a PIN below.";
        }
        catch(Exception){if(_loaded&&version==_statusVersion)_state.Text="PIN status could not be checked. You can use your password.";}
    }
    private void Clear()=>_password.Password=_pin.Password=_repeat.Password="";
    private async Task Run(bool remove)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current PIN operation.");
        var values=(Password:_password.Password,Pin:_pin.Password,Repeat:_repeat.Password);Clear();_statusVersion++;_cancellation=new();_host.IsEnabled=false;
        try
        {
            await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
            {
                var pins=new OwnerPins(store);if(remove)pins.Remove(owner,values.Password,token);else pins.Set(owner,values.Password,values.Pin,values.Repeat,token);return true;
            },token),_cancellation.Token);
            if(_loaded){_state.Text=remove?"PIN is not available here. Use your password or set a PIN below.":"PIN is enabled for this owner in this Windows profile.";_status.Text=remove?"PIN removed. You can unlock with your password.":"PIN saved. Lock the app, choose PIN, and enter it to unlock.";}
        }
        catch(OperationCanceledException){if(_loaded)_status.Text="PIN operation cancelled. Check your sign-in options before retrying.";}
        finally{Clear();_host.IsEnabled=true;_cancellation.Dispose();_cancellation=null;if(_loaded)_=ShowState(++_statusVersion);}
    }
}
