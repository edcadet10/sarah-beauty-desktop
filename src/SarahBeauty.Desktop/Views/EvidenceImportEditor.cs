using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class EvidenceImportEditor(BusinessStore store,OwnerSession owner,AttachmentVersion? previous,Func<StoredAttachment,Task> imported)
{
    private readonly StackPanel _panel=Form.Panel();
    private readonly TextBlock _status=Form.Text(),_sourceLabel=Form.Text("No source file selected.");
    private readonly TextBox _name=Form.Input("Managed file name",previous?.Name??""),_reason=Form.Input("Import / version reason"),_scope=Form.Input("Photo permission use / scope",previous?.PermissionUse??"");
    private readonly ComboBox _category=Form.Select("File category",["Receipt","Evidence","Customer photo"],previous?.Category??"Receipt");
    private ComboBox? _customer;
    private string _source="";
    private readonly string _request=Guid.NewGuid().ToString();
    private AttachmentImport? _baseline;
    private bool _completed;
    internal bool Busy {get;private set;}
    internal bool HasUnsavedFields=>!_completed && _baseline is not null && (_source.Length>0 || Read()!=_baseline);

    internal UIElement Build()
    {
        var customers=new CustomerBook(store).Search(owner,includeArchived:true);
        _customer=Form.Records("Photo subject customer",customers.Select(c=>(c.Id,c.FullName+" • "+c.PhotoPermission+" • "+c.PermissionScope)),previous?.CustomerId??"");
        _customer.SelectionChanged+=(_,_)=>{if(previous is null)_scope.Text=customers.SingleOrDefault(c=>c.Id==_customer.SelectedId())?.PermissionScope??"";};
        _category.SelectionChanged+=(_,_)=>EnablePhotoContext();EnablePhotoContext();_category.IsEnabled=previous is null;
        _panel.Add(Form.Title(previous is null?"Import a new file":$"Add version after v{previous.Number}"),_name,_category,_customer,_scope,_reason,
            Form.Action("Choose evidence file",_status,async()=>
            {
                if(Busy||_completed)throw new ArgumentException("Start a new file or replacement import before choosing another source.");
                string? source=await EvidenceFilePicker.Pick();if(source is null||!_panel.IsLoaded)return;
                _source=source;_sourceLabel.Text="Selected source: "+Path.GetFileName(source);if(_name.Text.Length==0)_name.Text=Path.GetFileNameWithoutExtension(source);
            }),_sourceLabel,_status,Form.Action("Import selected file",_status,Import));
        _baseline=Read();return _panel;
    }
    private void EnablePhotoContext()
    {
        bool photo=_category.SelectedItem as string=="Customer photo";if(_customer is not null)_customer.IsEnabled=photo && previous is null;_scope.IsEnabled=photo && previous is null;
    }
    private AttachmentImport Read()
    {
        string category=_category.SelectedItem as string??"Receipt";bool photo=category=="Customer photo";
        return new(_name.Text,category,_reason.Text,photo?_customer?.SelectedId()??"":"",photo?_scope.Text:"",previous?.AttachmentId??"",previous?.Number??0);
    }
    private async Task Import()
    {
        if(Busy)throw new ArgumentException("Wait for the current file import.");if(_completed)throw new ArgumentException("This import already completed. Start a new file or replacement import for another version.");
        if(_source.Length==0)throw new ArgumentException("Choose a source file first.");var draft=Read();string source=_source;Busy=true;
        try
        {
            var saved=await Task.Run(()=>new ManagedAttachments(store).Import(owner,source,draft,_request));_completed=true;
            _status.Text=$"Imported {saved.Version.Name} v{saved.Version.Number}. Its managed copy is saved. Use the file history below to review or link it."
                +(Read()!=draft?" Newer field edits were preserved; start a new import to use them.":"");
            try{await imported(saved);}catch(Exception error){_status.Text=$"Imported {saved.Version.Name} v{saved.Version.Number} successfully. File history could not refresh: {error.Message}";}
        }
        finally{Busy=false;}
    }
}
