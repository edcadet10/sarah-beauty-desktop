using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class EvidenceFileField(BusinessStore store,OwnerSession owner,ManagedEvidenceFile? captured)
{
    private readonly SearchableRecords _choice=new("Saved receipt or evidence file","None / external reference only");
    private readonly Dictionary<string,ManagedEvidenceFile> _files=[];
    private readonly TextBlock _status=Form.Text();
    internal UIElement Build()
    {
        var panel=Form.Panel();if(captured is not null)_files[captured.VersionId]=captured;Update(captured?.VersionId);
        panel.Add(Form.Text("Optional supporting file. Import it in Evidence files, then select its exact saved version here. Keep any required decision/evidence notes below."),
            _choice.Build(),_status,Form.Action("Load saved evidence files",_status,async()=>
            {
                var files=await Task.Run(()=>new ManagedAttachments(store).History(owner));string selected=_choice.SelectedId;
                foreach(var file in files.Where(f=>f.Version.Category!="Customer photo"))_files.TryAdd(file.Version.Id,ManagedEvidenceFile.From(file));
                Update(selected);
            }));return panel;
    }
    private void Update(string? selected)=>_choice.Update(_files.Values.Select(f=>(f.VersionId,$"{f.Name} • v{f.Number} • {f.Category} • {f.OriginalFileName}")),selected);
    internal ManagedEvidenceFile? Read()=>_choice.SelectedId.Length==0?null:_files.GetValueOrDefault(_choice.SelectedId)??throw new ArgumentException("Choose a loaded saved evidence file version.");
}
