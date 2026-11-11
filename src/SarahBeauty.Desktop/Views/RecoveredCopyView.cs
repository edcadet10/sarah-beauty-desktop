using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class RecoveredCopyView(StagedArchiveReview review)
{
    private readonly RecoveredStores _service=new();
    private readonly TextBox _reason=Form.Input("Recovery reason");
    private readonly PasswordBox _phrase=Form.Secret("New passphrase for recovered owner"),_repeat=Form.Secret("Repeat new recovered-owner passphrase");
    private readonly CheckBox _acknowledgement=new(){Content=Form.Text("I confirm that the previous primary is retired or permanently unavailable, and I will not keep posting in that previous copy."),MinHeight=44};
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _fields=new(),_result=new();
    private readonly string _request=Guid.NewGuid().ToString();
    private ComboBox? _owner;
    private CancellationTokenSource? _cancellation;
    private PreparedRecoveredStore? _prepared;
    internal UIElement Build()
    {
        var fields=Form.Panel();_owner=Form.Records("Owner whose access will be recovered",_service.Owners(review).Select(p=>(p.Id,p.Name)));
        fields.Add(Form.Text("Prepare a separate TEST working copy from this captured archive. Review its date and watermark first; later changes are outside this copy. Reestablish the selected owner's access with an easy-to-type word passphrase. Other owner credentials and historical approvals are preserved."),
            _owner,_reason,_phrase,_repeat,_acknowledgement,Form.Action("Prepare separate recovered TEST copy",_status,Prepare));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(_fields,Form.Action("Cancel recovered-copy preparation",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status,_result);
        panel.Unloaded+=(_,_)=>{_cancellation?.Cancel();Clear();};return new Expander{Header="Prepare a recovered TEST working copy",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private void Clear(){_phrase.Password="";_repeat.Password="";}
    private async Task Prepare()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current recovered-copy preparation.");if(_prepared is not null)throw new ArgumentException("This review already prepared a separate copy. Keep its recorded folder before starting another recovery.");
        string phrase=_phrase.Password;if(phrase!=_repeat.Password)throw new ArgumentException("Enter the same new recovered-owner passphrase twice.");
        var decision=new RestoreCopyDecision(_owner!.SelectedId(),_reason.Text,_acknowledgement.IsChecked==true);_cancellation=new();_fields.IsEnabled=false;_status.Text="Preparing and verifying the separate recovered copy…";
        try
        {
            _prepared=await _service.PrepareAsync(review,AppDataPaths.EnsureTestFolderExists(),decision,phrase,_request,_cancellation.Token);var r=_prepared.Receipt;
            _result.Content=ReportTable.Build(["Prepared recovery fact","Value"],[["Folder",_prepared.Folder],["Logical Store ID",r.StoreId],["New writable instance",r.InstanceId],["Known source instance",r.SourceInstanceId??"Not captured by this earlier archive"],
                ["Source watermark / time prepared",r.SourceWatermark+" / "+r.PreparedAtUtc],["Recovered owner",r.RecoveredOwnerId],["Source archive SHA-256",r.SourceArchiveHash],["Source manifest SHA-256",r.SourceManifestHash],["Reason",r.Reason]]);
            _status.Text="Recovered TEST copy prepared. The app still uses its previous working store. Keep the prepared folder for the separate activation step.";
        }
        catch(OperationCanceledException){_status.Text="Recovered-copy preparation cancelled. The working store is unchanged.";}
        finally{Clear();_fields.IsEnabled=_prepared is null;_cancellation.Dispose();_cancellation=null;}
    }
}
