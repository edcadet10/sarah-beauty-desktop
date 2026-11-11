using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class TaxPeriodView(BusinessStore store,OwnerSession owner)
{
    private readonly TaxPeriodBook _book=new(store);
    private readonly ListView _list=new(){MaxHeight=220};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Tax periods and filing status"),Form.Text("Enter confirmed reporting periods and due dates. Unknown due dates stay visible. Remittances and tax recoveries are recorded separately in Money; they never mark a return filed."),
            Form.Action("Add tax period",_status,()=>{Edit(new());return Task.CompletedTask;}),_status,_list,_detail);
        Refresh();
        _list.SelectionChanged+=(_,_)=>
        {
            try{if(_list.SelectedItem is TaxPeriod period)Review(period);}
            catch(Exception error){_status.Text=error.Message;}
        };
        return panel;
    }

    private void Refresh()=>_list.ItemsSource=_book.List(owner).OrderByDescending(p=>p.Through).ToList();

    private void Edit(TaxPeriod period)
    {
        _detail.Children.Clear();
        var name=Form.Input("Period name",period.Name);
        var jurisdiction=Form.Input("Confirmed jurisdiction / tax authority",period.Jurisdiction);
        var from=Form.Input("Period start (YYYY-MM-DD)",period.From?.ToString("yyyy-MM-dd")??"");
        var through=Form.Input("Period end (YYYY-MM-DD)",period.Through?.ToString("yyyy-MM-dd")??"");
        var due=Form.Input("Confirmed filing due date (YYYY-MM-DD; blank if unknown)",period.DueDate?.ToString("yyyy-MM-dd")??"");
        var status=Form.Text();
        _detail.Add(Form.Title("Tax period details"),name,jurisdiction,from,through,due,status,Form.Action("Save tax period",status,async()=>
        {
            var value=period with{Name=name.Text,Jurisdiction=jurisdiction.Text,From=Input.Date(from.Text),Through=Input.Date(through.Text),DueDate=Input.Date(due.Text)};
            var saved=await Task.Run(()=>_book.Save(owner,value));
            Refresh();Review(saved);
        }));
    }

    private void Review(TaxPeriod period)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title(period.Name),Form.Text(period.ToString()),Form.Text("Filing due: "+(period.DueDate?.ToString("yyyy-MM-dd")??"Unknown — confirm the actual due date")),
            Form.Action("Edit period details",status,()=>{Edit(period);return Task.CompletedTask;}));
        var names=new OwnerAccess(store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
        foreach(var item in period.History)
        {
            var d=item.Decision;
            _detail.Add(Form.Text($"{d.Status} • Filing date: {d.FilingDate?.ToString("yyyy-MM-dd")??"No filing recorded"} • Reported tax: {(d.ReportedTaxCents.HasValue?Money.Display(d.ReportedTaxCents.Value):"Unknown / not filed")}\nReference: {d.Reference} • Evidence: {d.EvidenceId}\n{d.Reason}\nRecorded {item.AtUtc} by {names.GetValueOrDefault(item.OwnerId,"Former owner")}"));
        }
        var fields=new TaxFilingFields();
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to record status");
        _detail.Add(Form.Title("Record a filing status decision"),fields.Build(),phrase,status,Form.Action("Record reviewed tax status",status,async()=>
        {
            var decision=fields.Read(period.Revision);
            string password=phrase.Password;phrase.Password="";
            var saved=await Task.Run(()=>_book.RecordStatus(owner,period.Id,decision,password));
            Refresh();Review(saved);
        }));
    }
}
