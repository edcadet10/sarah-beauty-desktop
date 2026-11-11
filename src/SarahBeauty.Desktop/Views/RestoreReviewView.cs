using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty_Desktop.Views;

internal sealed class RestoreReviewView(string? archive=null,CreatedArchive? expected=null)
{
    private string? _archive=archive,_recovery;
    private readonly TextBlock _archiveLabel=Form.Text(archive is null?"Encrypted archive: None selected.":"Encrypted archive: "+Path.GetFileName(archive));
    private readonly TextBlock _recoveryLabel=Form.Text("Protected recovery file: None selected."),_status=Form.Text();
    private readonly PasswordBox _phrase=Form.Secret("Recovery passphrase for archive review");
    private readonly ContentControl _fields=new(),_details=new();
    private CancellationTokenSource? _cancellation;
    private StagedArchiveReview? _review;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(Form.Text("Select an encrypted archive and its separately saved protected recovery file. Review decrypts into a private temporary workspace and checks the database and every managed file."),
            _archiveLabel,Form.Action("Choose encrypted archive",_status,async()=>{var path=await RecoveryFilePicker.OpenArchive();if(path is not null){_archive=path;_archiveLabel.Text="Encrypted archive: "+Path.GetFileName(path);}}),
            _recoveryLabel,Form.Action("Choose protected recovery file",_status,async()=>{var path=await RecoveryFilePicker.OpenRecovery();if(path is not null){_recovery=path;_recoveryLabel.Text="Protected recovery file: "+Path.GetFileName(path);}}),
            _phrase,Form.Action("Decrypt and validate archive for review",_status,Review));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Title("Review encrypted recovery archive"),_fields,Form.Action("Cancel archive review",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),
            Form.Action("Close staged archive review",_status,()=>{if(_cancellation is not null)throw new ArgumentException("Cancel and wait for the current review first.");Close();_status.Text="Staged archive review closed.";return Task.CompletedTask;}),_status,_details);
        panel.Unloaded+=(_,_)=>{_cancellation?.Cancel();_phrase.Password="";Close();};return panel;
    }
    private async Task Review()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current archive review.");string source=_archive??throw new ArgumentException("Choose an encrypted archive.");string key=_recovery??throw new ArgumentException("Choose its protected recovery file.");string phrase=_phrase.Password;
        _cancellation=new();_fields.IsEnabled=false;_status.Text="Decrypting and validating the isolated archive…";
        try
        {
            string parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SarahBeautyDesktop","RecoveryReview");Directory.CreateDirectory(parent);
            byte[] material=await RecoveryMaterialFiles.ReadAsync(key,_cancellation.Token);var staged=await new ArchiveRestores().StageAsync(source,parent,material,phrase,expected,_cancellation.Token);
            try{_cancellation.Token.ThrowIfCancellationRequested();var view=RestoreReviewFactsView.Build(staged);Close();_review=staged;_details.Content=view;}
            catch{staged.Dispose();throw;}
            _status.Text="Archive validation completed. Review the captured facts; the working store is unchanged.";
        }
        catch(OperationCanceledException){_status.Text="Archive review cancelled. The working store is unchanged.";}
        finally{_phrase.Password="";_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
    private void Close(){_review?.Dispose();_review=null;_details.Content=null;}
}
