using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class LiveBusinessCreationView(string parent,Action<CreatedLiveBusiness> created)
{
    private readonly BusinessProfileFields _profile=new();
    private readonly TextBox _first=Form.Input("First LIVE owner name"),_second=Form.Input("Second LIVE owner name"),_reason=Form.Input("Private reason for creating this LIVE business");
    private readonly PasswordBox _firstPhrase=Form.Secret("First LIVE owner's passphrase"),_firstRepeat=Form.Secret("Repeat first LIVE owner's passphrase"),_secondPhrase=Form.Secret("Second LIVE owner's passphrase"),_secondRepeat=Form.Secret("Repeat second LIVE owner's passphrase");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _controls=new();
    private readonly StackPanel _panel=Form.Panel();
    private CancellationTokenSource? _cancellation;
    private ContentDialog? _dialog;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(_profile.Build(),_first,_firstPhrase,_firstRepeat,_second,_secondPhrase,_secondRepeat,_reason,Form.Action("Review clean LIVE creation",_status,Review));_profile.Set(new(),[]);_controls.Content=fields;
        _panel.Add(Form.Title("Create a clean LIVE business"),Form.Text("Enter the new business settings and two distinct owners. Choose a separate word passphrase of 15 to 256 characters for each owner. Currency, time zone, books start and new-business scope need explicit review. Leave unknown legal, EIN, banking and tax facts unconfirmed; complete operational reviews before posting. Ownership percentages and opening balances are not inferred."),_controls,
            Form.Action("Cancel LIVE creation operation",_status,()=>{Cancel();return Task.CompletedTask;}),_status);
        _panel.Unloaded+=(_,_)=>Cancel();return _panel;
    }
    private void Clear(){_firstPhrase.Password="";_firstRepeat.Password="";_secondPhrase.Password="";_secondRepeat.Password="";}
    private void Cancel(){Clear();_dialog?.Hide();_cancellation?.Cancel();}
    private CreationValues Read()
    {
        if(_firstPhrase.Password!=_firstRepeat.Password||_secondPhrase.Password!=_secondRepeat.Password)throw new ArgumentException("Repeat each LIVE owner's passphrase exactly.");
        string first=_first.Text.Trim(),second=_second.Text.Trim(),firstPhrase=_firstPhrase.Password,secondPhrase=_secondPhrase.Password;var change=LiveBusinessCreationRules.Review(_profile.Read(),first,firstPhrase,second,secondPhrase,_reason.Text);return new(change,first,firstPhrase,second,secondPhrase);
    }
    private sealed record CreationValues(BusinessProfileChange Change,string First,string FirstPhrase,string Second,string SecondPhrase)
    {
        public override string ToString()=>"Private LIVE creation values";
    }
}
