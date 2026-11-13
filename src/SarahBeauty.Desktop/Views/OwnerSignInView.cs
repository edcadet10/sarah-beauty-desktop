using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal sealed class OwnerSignInView(OwnerAccess access,OwnerPins pins,Action<OwnerSession> signedIn)
{
    private readonly StackPanel _panel=Form.Panel();
    private readonly ContentControl _host=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly ComboBox _owner=new(){Header="Owner"};
    private readonly ComboBox _method=Form.Select("Unlock with",["Password / passphrase","PIN"],"Password / passphrase");
    private readonly PasswordBox _phrase=Form.Secret("Passphrase"),_pin=PinInput.Create("PIN");
    private readonly TextBlock _hint=Form.Text(),_status=Form.Text();
    private List<(string Id,string Name)> _owners=[];

    internal UIElement Build()
    {
        _owners=access.Owners();_owner.ItemsSource=_owners.Select(o=>o.Name).ToList();_owner.SelectedIndex=0;
        _panel.MaxWidth=440;_panel.HorizontalAlignment=HorizontalAlignment.Left;
        _panel.Add(Form.Title("Welcome to Sarah Beauty"),_owner,_method,_hint,_phrase,_pin,_status,Form.PrimaryAction("Sign in",_status,SignIn));
        _owner.SelectionChanged+=(_,_)=>{Clear();_status.Text="";};_method.SelectionChanged+=(_,_)=>ShowMethod();
        _panel.Unloaded+=(_,_)=>Clear();ShowMethod();_host.Content=_panel;return _host;
    }
    private void ShowMethod()
    {
        Clear();_status.Text="";bool pin=UsesPin;
        _phrase.Visibility=pin?Visibility.Collapsed:Visibility.Visible;_pin.Visibility=pin?Visibility.Visible:Visibility.Collapsed;
        _hint.Text=pin?"Use the PIN you set in Owner security for this Windows profile. You can always choose your password instead.":"Enter your owner password or word-based passphrase.";
    }
    private bool UsesPin=>_method.SelectedItem as string=="PIN";
    private void Clear()=>_phrase.Password=_pin.Password="";
    private async Task SignIn()
    {
        if(_owner.SelectedIndex<0)throw new ArgumentException("Select an owner.");
        string id=_owners[_owner.SelectedIndex].Id,secret=UsesPin?_pin.Password:_phrase.Password;bool pin=UsesPin;Clear();_host.IsEnabled=false;
        try
        {
            var session=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
            {
                if(!pin)return access.SignIn(id,secret);
                if(!pins.IsConfigured(id))throw new UnauthorizedAccessException("A PIN is not available for this owner here. Sign in with your password, then set a PIN in Owner security.");
                return pins.SignIn(id,secret);
            },token));
            signedIn(session);
        }
        finally{Clear();_host.IsEnabled=true;}
    }
}
