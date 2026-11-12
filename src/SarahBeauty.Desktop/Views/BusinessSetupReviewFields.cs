using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Setup;

namespace SarahBeauty_Desktop.Views;

internal sealed class BusinessSetupReviewFields(string label)
{
    private readonly ComboBox _state=Form.Select(label+" review state",["Unconfirmed","In progress","Confirmed","Not applicable"],"Unconfirmed");
    private readonly TextBox _reason=Form.Input(label+" private review reason");
    private readonly SearchableRecords _evidence=new(label+" supporting file","No supporting file selected");
    private StoredAttachment[] _files=[];
    internal UIElement Build(){_reason.AcceptsReturn=true;_reason.MinHeight=70;var panel=Form.Panel();panel.Add(Form.Title(label+" review"),_state,_reason,_evidence.Build());return panel;}
    internal void Set(BusinessSetupReview value,IEnumerable<StoredAttachment> files){_state.SelectedItem=Label(value.State);_reason.Text=value.Reason;UpdateFiles(files,value.Evidence?.VersionId??"");}
    internal void UpdateFiles(IEnumerable<StoredAttachment> files,string? selected=null)
    {
        _files=files.ToArray();_evidence.Update(_files.Select(f=>(f.Version.Id,$"{f.Version.Name} • v{f.Version.Number} • {f.Version.OriginalFileName}")),selected??_evidence.SelectedId);
    }
    internal BusinessSetupReview Read()
    {
        var state=_state.SelectedItem?.ToString() switch{"Unconfirmed"=>BusinessSetupState.Unconfirmed,"In progress"=>BusinessSetupState.InProgress,"Confirmed"=>BusinessSetupState.Confirmed,"Not applicable"=>BusinessSetupState.NotApplicable,_=>throw new ArgumentException("Choose a "+label.ToLowerInvariant()+" review state.")};
        var evidence=_evidence.SelectedId.Length==0?null:ManagedEvidenceFile.From(_files.Single(f=>f.Version.Id==_evidence.SelectedId));return new(state,_reason.Text,evidence);
    }
    internal static string Label(BusinessSetupState state)=>state switch{BusinessSetupState.Unconfirmed=>"Unconfirmed",BusinessSetupState.InProgress=>"In progress",BusinessSetupState.Confirmed=>"Confirmed",BusinessSetupState.NotApplicable=>"Not applicable",_=>throw new ArgumentException("Unsupported setup review state.")};
}
