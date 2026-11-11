using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class EvidenceView(BusinessStore store,OwnerSession owner)
{
    private readonly ManagedAttachments _files=new(store);
    private readonly SearchableRecords _library=new("Managed file version");
    private readonly ContentControl _import=new(),_detail=new();
    private readonly TextBlock _status=Form.Text();
    private readonly StackPanel _panel=Form.Panel();
    private EvidenceImportEditor? _importEditor;
    private EvidenceAssociationView? _associations;
    private AttachmentVersion? _selected;

    internal UIElement Build()
    {
        _panel.Add(Form.Title("Evidence files"),Form.Text("Keep private copies of receipts, evidence, and permission-reviewed customer photos. Supported: PDF, JPEG, PNG, up to 50 MB. Importing a file does not approve money or upload it."),
            Form.Action("Start a new file import",_status,()=>BeginImport(null)),_import,_status,_library.Build(),
            Form.Action("Refresh managed file history",_status,Refresh),Form.Action("Show selected file version",_status,Show),
            Form.Action("Add a replacement file version",_status,async()=>
            {
                if(_selected is null)throw new ArgumentException("Show an imported file version first.");
                var latest=_files.History(owner,_selected.AttachmentId).Last();if(latest.Version.Id!=_selected.Id)throw new ArgumentException("Choose the latest version from file history before adding a replacement.");
                await BeginImport(_selected);
            }),_detail);
        _library.Update(Choices());return _panel;
    }
    private IEnumerable<(string Id,string Label)> Choices()=>_files.History(owner).OrderByDescending(f=>f.Version.AtUtc)
        .Select(f=>(f.Version.Id,$"{f.Version.Name} • v{f.Version.Number} • {f.Version.Category} • {f.Version.OriginalFileName}"));
    private Task Refresh(){_library.Update(Choices());return Task.CompletedTask;}
    private async Task BeginImport(AttachmentVersion? previous)
    {
        if(_importEditor?.Busy==true)throw new ArgumentException("Wait for the current file import before starting another.");
        if(_importEditor?.HasUnsavedFields==true && !await Discard("Discard this unsaved file import?"))return;
        _importEditor=new(store,owner,previous,async saved=>{await Refresh();if(_library.SelectedId.Length==0)_library.Update(Choices(),saved.Version.Id);_status.Text=$"Saved managed file version {saved.Version.Id}.";});
        _import.Content=_importEditor.Build();
    }
    private async Task Show()
    {
        string id=_library.SelectedId;if(id.Length==0)throw new ArgumentException("Choose a managed file version.");
        if(_associations?.Busy==true)throw new ArgumentException("Wait for the current evidence decision before changing its file.");
        if(_associations?.HasUnsavedFields==true && !await Discard("Discard unsaved evidence link fields?"))return;
        var editor=_associations;string fields=editor?.Fingerprint??"";var file=await Task.Run(()=>_files.Get(owner,id));if(id!=_library.SelectedId)return;
        if(!ReferenceEquals(editor,_associations)||fields!=(_associations?.Fingerprint??"")){_status.Text="Evidence fields changed during loading. Finish or discard them before showing another file.";return;}
        var v=file.Version;_selected=v;_associations=new(store,owner,v);var panel=Form.Panel();
        panel.Add(Form.Title(v.Name+" • v"+v.Number),ReportTable.Build(["Retained file fact","Value"],
            [["Version identity",v.Id],["Attachment identity",v.AttachmentId],["Previous version",v.PreviousVersionId],["Category",v.Category],["Original file name",v.OriginalFileName],
            ["Media type",v.MediaType],["Byte length",v.ByteLength.ToString()],["Content SHA-256",v.Sha256],["Metadata SHA-256",file.SnapshotHash],["Reason",v.Reason],
            ["Owner / time",v.CreatedBy+" • "+v.AtUtc],["Photo subject",v.CustomerId],["Permission identity / use",v.PermissionId+" • "+v.PermissionUse]]),
            EvidenceContentView.Build(store,owner,v),_associations.Build());_detail.Content=panel;
    }
    private async Task<bool> Discard(string title)=>await new ContentDialog{XamlRoot=_panel.XamlRoot,Title=title,Content="Saved files and evidence decisions remain in history.",
        PrimaryButtonText="Discard fields",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close}.ShowAsync()==ContentDialogResult.Primary;
}
