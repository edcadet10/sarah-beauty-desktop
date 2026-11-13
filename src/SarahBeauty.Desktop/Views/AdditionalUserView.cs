using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class AdditionalUserView(BusinessStore store,OwnerSession owner)
{
    private readonly TextBox _username=Form.Input("New username");
    private readonly PasswordBox _password=Form.Secret("New user's password"),_current=Form.Secret("Your current password");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _fields=new();

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(Form.Text("You can add the other business user when you are ready. This gives them their own sign-in; ownership percentages are managed separately."),
            _username,_password,Form.Muted("Use 15 or more characters for their password."),_current,Form.Action("Add user",_status,Add));
        _fields.Content=panel;var content=Form.Panel();content.Add(_fields,_status);
        content.Unloaded+=(_,_)=>Leave();
        return new Expander{Header="Add another user later",Content=content,HorizontalAlignment=Microsoft.UI.Xaml.HorizontalAlignment.Stretch};
    }

    internal void Leave()=>_password.Password=_current.Password="";

    private async Task Add()
    {
        if(!_fields.IsEnabled)return;
        var values=(Username:_username.Text,Password:_password.Password,Current:_current.Password);
        _fields.IsEnabled=false;
        try
        {
            await ((App)Microsoft.UI.Xaml.Application.Current).Operations.RunAsync(token=>Task.Run(()=>new OwnerAccess(store).AddUser(owner,values.Current,values.Username,values.Password),token));
            _status.Text="User added. Lock the workspace to sign in with their account.";_username.Text="";
        }
        finally{Leave();_fields.IsEnabled=true;}
    }
}
