using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class RecoverySetupView(BusinessStore store,OwnerSession owner,Action confirmed)
{
    private readonly PasswordBox _phrase=Form.Secret("Recovery passphrase (15–256 characters)");
    private readonly PasswordBox _repeat=Form.Secret("Repeat recovery passphrase for a new file");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _fields=new();
    private CancellationTokenSource? _cancellation;
    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(Form.Text("Use an easy-to-type word passphrase. Keep it separately from the protected recovery file, and keep that file separately from your archives. Retain older recovery files for older archives after a key change."),_phrase,_repeat,
            Form.Action("Create and save recovery file",_status,()=>Run(Create)),Form.Action("Confirm existing recovery file",_status,()=>Run(ConfirmExisting)));
        _fields.Content=panel;var outer=Form.Panel();outer.Add(_fields,Form.Action("Cancel recovery setup",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status);
        outer.Unloaded+=(_,_)=>{_cancellation?.Cancel();Clear();};return new Expander{Header="Portable recovery setup",Content=outer,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private async Task Run(Func<string,CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current recovery setup operation.");string phrase=_phrase.Password;
        _cancellation=new();_fields.IsEnabled=false;
        try{await action(phrase,_cancellation.Token);}
        finally{Clear();_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
    private void Clear(){_phrase.Password="";_repeat.Password="";}
    private async Task Create(string phrase,CancellationToken cancellation)
    {
        if(phrase!=_repeat.Password)throw new ArgumentException("Enter the same recovery passphrase twice for a new file.");
        if(phrase.EnumerateRunes().Count() is <15 or >256)throw new ArgumentException("Use a word passphrase with 15–256 characters.");
        string? destination=await RecoveryFilePicker.Save("SarahBeauty-recovery-"+DateTime.Today.ToString("yyyyMMdd"),".sbrecovery","Protected recovery file");if(destination is null){_status.Text="Recovery file selection cancelled.";return;}
        _status.Text="Creating the protected recovery file…";var material=await new RecoveryCipher().CreateAsync(phrase,cancellation);await RecoveryMaterialFiles.SaveAsync(material,destination,cancellation);
        try{await Register(destination,phrase,cancellation);}
        catch(Exception error){throw new IOException("The protected recovery file was saved, but confirmation did not finish. Choose Confirm existing recovery file with that saved file.",error);}
    }
    private async Task ConfirmExisting(string phrase,CancellationToken cancellation)
    {
        string? source=await RecoveryFilePicker.OpenRecovery();if(source is null){_status.Text="Recovery file selection cancelled.";return;}await Register(source,phrase,cancellation);
    }
    private async Task Register(string source,string phrase,CancellationToken cancellation)
    {
        var bytes=await RecoveryMaterialFiles.ReadAsync(source,cancellation);var profile=await new RecoveryProfiles(store).RegisterAsync(owner,bytes,phrase,cancellation);
        _status.Text="Portable recovery confirmed • Recipient fingerprint "+profile.RecipientFingerprint;
        try{confirmed();}catch(Exception){_status.Text+=". Refresh backup health to reload the saved confirmation.";}
    }
}
