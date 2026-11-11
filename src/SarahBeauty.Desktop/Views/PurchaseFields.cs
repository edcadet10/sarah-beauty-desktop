using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class PurchaseFields
{
    private readonly TextBox _name,_reference,_date,_service,_due,_evidence;
    private readonly ComboBox _supplier;
    private readonly CheckBox _reliable;
    private readonly List<PurchaseLine> _lines;
    private readonly StackPanel _panel=Form.Panel();

    internal PurchaseFields(BusinessStore store,OwnerSession owner,Purchase value)
    {
        _name=Form.Input("Bill description",value.Name);
        _supplier=Form.Records("Supplier",new BusinessResources(store).Suppliers(owner).Select(s=>(s.Id,s.Name)),value.SupplierId);
        _reference=Form.Input("Supplier invoice reference",value.SupplierReference);
        _reliable=new CheckBox{Content="This reference uniquely identifies the supplier invoice",IsChecked=value.ReliableReference};
        _date=Form.Input("Actual purchase date (YYYY-MM-DD)",value.PurchaseDate?.ToString("yyyy-MM-dd")??"");
        _service=Form.Input("Service date (optional)",value.ServiceDate?.ToString("yyyy-MM-dd")??"");
        _due=Form.Input("Due date (optional)",value.DueDate?.ToString("yyyy-MM-dd")??"");
        _evidence=Form.Input("Bill evidence reference (optional; missing evidence stays visible)",value.EvidenceId);
        _lines=value.Lines.ToList();
        _panel.Add(_name,_supplier,_reference,_reliable,_date,_service,_due,_evidence,LineEditor(store,owner));
    }

    internal UIElement Build()=>_panel;
    internal Purchase Read(Purchase previous)=>previous with{Name=_name.Text,SupplierId=_supplier.SelectedId(),SupplierReference=_reference.Text,
        ReliableReference=_reliable.IsChecked==true,PurchaseDate=Input.Date(_date.Text),ServiceDate=Input.Date(_service.Text),DueDate=Input.Date(_due.Text),
        EvidenceId=_evidence.Text.Trim(),Lines=_lines.ToArray()};

    private UIElement LineEditor(BusinessStore store,OwnerSession owner)
    {
        var panel=Form.Panel();
        var list=new ListView{MaxHeight=240,ItemsSource=_lines.ToList()};
        var editor=Form.Panel();
        var status=Form.Text();
        void Keep(PurchaseLine line){_lines.RemoveAll(l=>l.Id==line.Id);_lines.Add(line);list.ItemsSource=_lines.ToList();}
        panel.Add(Form.Text("Purchase lines. Save the whole bill after keeping any line changes."),list,status,
            Form.Action("Add purchase line",status,()=>
            {
                PurchaseLineFields.Edit(editor,store,owner,new(Guid.NewGuid().ToString(),"",1,0,null,null,""),Keep);
                return Task.CompletedTask;
            }),Form.Action("Edit selected line",status,()=>
            {
                if(list.SelectedItem is not PurchaseLine line)throw new ArgumentException("Choose a line.");
                PurchaseLineFields.Edit(editor,store,owner,line,Keep);
                return Task.CompletedTask;
            }),Form.Action("Remove selected draft line",status,()=>
            {
                if(list.SelectedItem is not PurchaseLine line)throw new ArgumentException("Choose a line.");
                _lines.RemoveAll(l=>l.Id==line.Id);list.ItemsSource=_lines.ToList();
                return Task.CompletedTask;
            }),editor);
        return panel;
    }
}
