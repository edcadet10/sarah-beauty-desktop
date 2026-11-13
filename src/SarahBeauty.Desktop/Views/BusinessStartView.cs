using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using Windows.System;

namespace SarahBeauty_Desktop.Views;

internal sealed class BusinessStartView(
    Func<string,string,string,CancellationToken,(BusinessStore Store,OwnerSession Owner)> create,
    Action<(BusinessStore Store,OwnerSession Owner)> started)
{
    private readonly TextBox _name=Form.Input("Business name"),_username=Form.Input("Username");
    private readonly PasswordBox _password=Form.Secret("Password");
    private readonly ContentControl _fields=new();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var form=Form.Panel();form.Add(_name,_username,_password,
            Form.Muted("Use 15 or more characters. A few memorable words work well."),
            Form.PrimaryAction("Create my workspace",_status,Create));
        _fields.Content=form;
        var panel=Form.Panel();panel.MaxWidth=480;panel.HorizontalAlignment=HorizontalAlignment.Center;
        panel.Add(Form.Title("Welcome to Sarah Beauty"),Form.Text("Enter these three details to get started. You can fill in everything else later in Business settings."),_fields,_status);
        _password.KeyDown+=CreateWithEnter;
        panel.Unloaded+=(_,_)=>_password.Password="";
        return panel;
    }

    private async void CreateWithEnter(object sender,KeyRoutedEventArgs args)
    {
        if(args.Key!=VirtualKey.Enter||args.KeyStatus.WasKeyDown||!_fields.IsEnabled)return;
        args.Handled=true;
        try{await Create();}catch(Exception error){_status.Text=error.Message;}
    }

    private async Task Create()
    {
        if(!_fields.IsEnabled)return;
        var values=(Name:_name.Text,Username:_username.Text,Password:_password.Password);
        _fields.IsEnabled=false;_status.Text="Creating your local workspace…";
        try
        {
            var result=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>create(values.Name,values.Username,values.Password,token),token));
            _password.Password="";
            started(result);
        }
        finally{_fields.IsEnabled=true;}
    }
}
