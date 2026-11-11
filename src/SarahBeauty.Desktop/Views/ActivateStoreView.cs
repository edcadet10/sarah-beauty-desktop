using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty_Desktop.Views;

internal sealed class ActivateStoreView(string parent,Action<ActivatedTestStore> activated)
{
    private readonly SearchableRecords _choice=new("Prepared recovered copy");
    private readonly TextBlock _status=Form.Text();
    private readonly PasswordBox _phrase=Form.Secret("Recovered owner's passphrase to activate");
    private readonly ContentControl _details=new(),_fields=new();
    private readonly StackPanel _panel=Form.Panel();
    private IReadOnlyList<PreparedStoreCandidate> _copies=[];
    private PreparedStoreCandidate? _selected;
    private CancellationTokenSource? _cancellation;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(Form.Text("Choose the separately prepared TEST copy and sign in with the reestablished owner's passphrase. An accessible previous local primary of the same business must be retired first. Stored copies are preserved."),
            _choice.Build(),Form.Action("Refresh prepared copies",_status,Refresh),Form.Action("Show selected recovered copy",_status,Show),_details,_phrase,Form.Action("Activate selected recovered copy",_status,Activate));_fields.Content=fields;
        _panel.Add(Form.Title("Activate a prepared recovered copy"),_fields,Form.Action("Cancel recovered-store activation",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status);
        _panel.Loaded+=async(_,_)=>{try{await Refresh();}catch(Exception error){_status.Text=error.Message;}};_panel.Unloaded+=(_,_)=>{_cancellation?.Cancel();_phrase.Password="";};return _panel;
    }
    private async Task Refresh()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current recovered-store activation.");string selected=_choice.SelectedId;
        var copies=await Task.Run(()=>new PreparedStoreCatalog(parent).List());if(!_panel.IsLoaded)return;_copies=copies;_choice.Update(copies.Select(c=>(c.RequestId,$"{c.BusinessName} • {c.Status} • Source watermark {c.SourceWatermark} • {c.PreparedAtUtc}")),selected);
        if(copies.Count==0)_status.Text="No prepared copies were found. Review a complete encrypted archive and prepare a TEST copy first.";
    }
    private Task Show()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current activation.");if(_phrase.Password.Length>0)throw new ArgumentException("Clear the entered passphrase before showing a different copy.");
        _selected=_copies.SingleOrDefault(c=>c.RequestId==_choice.SelectedId)??throw new ArgumentException("Choose a prepared recovered copy.");var c=_selected;
        _details.Content=ReportTable.Build(["Recovered-copy fact","Value"],[["Business",c.BusinessName],["Status",c.Status],["Recovered owner",c.OwnerName],["Source watermark",c.SourceWatermark.ToString()],["Prepared time",c.PreparedAtUtc],["Instance identity",c.InstanceId],["Request identity",c.RequestId]]);return Task.CompletedTask;
    }
    private async Task Activate()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current activation.");var selected=_selected??throw new ArgumentException("Show the recovered copy before activating it.");
        if(selected.RequestId!=_choice.SelectedId)throw new ArgumentException("The selected copy changed. Show it before entering its passphrase.");if(!selected.CanActivate)throw new ArgumentException("Prepare a new copy from the verified archive with the current app. The earlier copy remains preserved.");
        string phrase=_phrase.Password;_cancellation=new();_fields.IsEnabled=false;_status.Text="Verifying and selecting the recovered working store…";
        try{var result=await Task.Run(()=>new ActiveTestStores(parent).ActivateAsync(selected.RequestId,selected.OwnerId,phrase,_cancellation.Token));activated(result);}
        catch(OperationCanceledException){_status.Text="Recovered-store activation cancelled.";}
        finally{_phrase.Password="";_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
}
