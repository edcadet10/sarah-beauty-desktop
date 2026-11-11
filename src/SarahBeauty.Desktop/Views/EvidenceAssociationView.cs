using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class EvidenceAssociationView(BusinessStore store,OwnerSession owner,AttachmentVersion version)
{
    private readonly EvidenceLinks _links=new(store);
    private readonly ComboBox _kind=Form.Select("Evidence record type",Enum.GetValues<EvidenceTargetKind>().Select(EvidenceTargets.Label).ToArray(),"Customer");
    private readonly SearchableRecords _target=new("Evidence target record");
    private readonly TextBox _purpose=Form.Input("Evidence link purpose"),_releaseReason=Form.Input("Evidence release reason");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _history=new();
    private readonly ComboBox _release=Form.Records("Evidence link to release",[]);
    private EvidenceTarget[] _targets=[];
    private EvidenceLinkDraft? _lastDraft;
    private string _linkRequest=Guid.NewGuid().ToString(),_releaseRequest=Guid.NewGuid().ToString(),_releaseKey="";
    private string _savedLinkFields="";
    internal bool Busy {get;private set;}
    internal string Fingerprint=>LinkFields()+"\n"+_release.SelectedId()+"\n"+_releaseReason.Text;
    internal bool HasUnsavedFields=>_purpose.Text.Length>0 && LinkFields()!=_savedLinkFields || _releaseReason.Text.Length>0;
    private string LinkFields()=>_kind.SelectedItem+"\n"+_target.SelectedId+"\n"+_purpose.Text;

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(Form.Title("Link this exact file version"),Form.Text("Links retain this version and the record facts reviewed here. A replacement file does not update existing links. Linking does not approve money or alter an issued document."),
            _kind,_target.Build(),Form.Action("Load evidence target choices",_status,async()=>
            {
                var kind=Kind();var targets=await Task.Run(()=>new EvidenceTargets(store).List(owner,kind));if(kind!=Kind())return;
                _targets=targets.ToArray();_target.Update(_targets.Select(t=>(t.Id,t.Name+" • "+t.Status+" • revision "+t.Revision+(t.Archived?" • Archived":""))));
            }),_purpose,_status,Form.Action("Link reviewed file version",_status,Link),_history,_release,_releaseReason,Form.Action("Release selected evidence link",_status,Release));
        RefreshHistory();return panel;
    }
    private EvidenceTargetKind Kind()=>Enum.GetValues<EvidenceTargetKind>().Single(k=>EvidenceTargets.Label(k)==_kind.SelectedItem as string);
    private async Task Link()
    {
        if(Busy)throw new ArgumentException("Wait for the current evidence decision.");var target=_targets.SingleOrDefault(t=>t.Id==_target.SelectedId && t.Kind==Kind())??throw new ArgumentException("Load and choose a target from the selected record type.");
        var draft=new EvidenceLinkDraft(version.Id,target.Kind,target.Id,target.Revision,_purpose.Text);string fields=LinkFields();if(_lastDraft!=draft){_lastDraft=draft;_linkRequest=Guid.NewGuid().ToString();}Busy=true;
        try{var saved=await Task.Run(()=>_links.Link(owner,draft,_linkRequest));_savedLinkFields=fields;RefreshHistory();_status.Text=$"Evidence decision saved • {saved.Link.Id} • {(saved.Release is null?"Active link":"Already released")}.";}
        finally{Busy=false;}
    }
    private async Task Release()
    {
        if(Busy)throw new ArgumentException("Wait for the current evidence decision.");string id=_release.SelectedId(),reason=_releaseReason.Text;if(id.Length==0)throw new ArgumentException("Choose an active evidence link to release.");
        string key=id+"\n"+reason;if(key!=_releaseKey){_releaseKey=key;_releaseRequest=Guid.NewGuid().ToString();}Busy=true;
        try{await Task.Run(()=>_links.Release(owner,id,reason,_releaseRequest));if(_releaseReason.Text==reason && _release.SelectedId()==id)_releaseReason.Text="";RefreshHistory();_status.Text="Evidence link released. The association and file remain in history.";}
        finally{Busy=false;}
    }
    private void RefreshHistory()
    {
        var history=_links.History(owner,version.Id);_history.Content=PagedReport.Build("File evidence link history",
            ["State","Target type / name at capture","Target ID / revision","Purpose","Owner / time","Link identity / hash","Release reason / owner / time"],
            history.Select(h=>new[]{h.Release is null?"Active":"Released",EvidenceTargets.Label(h.Link.Target.Kind)+" • "+h.Link.Target.Name,h.Link.Target.Id+" • "+h.Link.Target.Revision,h.Link.Purpose,
                h.Link.OwnerId+" • "+h.Link.AtUtc,h.Link.Id+" • "+h.SnapshotHash,h.Release is null?"":h.Release.Reason+" • "+h.Release.OwnerId+" • "+h.Release.AtUtc}));
        string selected=_release.SelectedId();var choices=Form.Records("Evidence link to release",history.Where(h=>h.Release is null).Select(h=>(h.Link.Id,EvidenceTargets.Label(h.Link.Target.Kind)+" • "+h.Link.Target.Name)),selected);
        _release.ItemsSource=choices.ItemsSource;_release.SelectedItem=choices.SelectedItem;
    }
}
