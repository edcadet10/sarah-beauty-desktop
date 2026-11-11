using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class CreditNoteView(BusinessStore store,OwnerSession owner)
{
    private readonly CreditNoteBook _book=new(store);
    private readonly QuoteBook _documents=new(store);
    private readonly ListView _drafts=new(){MaxHeight=160};
    private readonly ListView _issued=new(){MaxHeight=180};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var invoices=_documents.Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void").ToList();
        var selected=Form.Records("Original invoice",invoices.Select(i=>(i.Id,i.ToString())));
        panel.Add(Form.Title("Invoice credit notes"),
            Form.Text("A credit note reduces an invoiced charge. It does not refund cash or create funded customer credit. Unapply affected payments before crediting paid charges."),
            selected,Form.Action("Prepare credit note",_status,()=>
            {
                if(selected.SelectedId().Length==0)throw new ArgumentException("Choose an original invoice.");
                Edit(new CreditNoteDraft{InvoiceVersionId=selected.SelectedId(),IssueDate=DateOnly.FromDateTime(DateTime.Today)});
                return Task.CompletedTask;
            }),_status,Form.Text("Saved drafts"),_drafts,Form.Text("Issued credit notes"),_issued,_detail);
        _drafts.SelectionChanged+=(_,_)=>{if(_drafts.SelectedItem is CreditNoteDraft draft)Review(draft);};
        _issued.SelectionChanged+=(_,_)=>{if(_issued.SelectedItem is DocumentVersion version)Show(version);};
        Refresh();
        return panel;
    }

    private void Refresh()
    {
        _drafts.ItemsSource=_book.Drafts(owner).Where(d=>d.Status=="Draft").ToList();
        _issued.ItemsSource=_documents.Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.CreditNote).ToList();
    }

    private void Edit(CreditNoteDraft draft)
    {
        _detail.Children.Clear();
        var reason=Form.Input("Client-facing reason for the reduction",draft.Name);
        var date=Form.Input("Credit date (YYYY-MM-DD)",draft.IssueDate?.ToString("yyyy-MM-dd")??"");
        var status=Form.Text();
        var quantities=new List<(string Charge,TextBox Input)>();
        _detail.Add(reason,date,Form.Text("Enter quantities to reduce. Enter zero to leave a charge unchanged."));
        foreach(var line in _book.Remaining(owner,draft.InvoiceVersionId))
        {
            decimal quantity=draft.Portions.SingleOrDefault(p=>p.ChargeId==line.ChargeId)?.Quantity??0;
            var input=Form.Input($"{line.Description} • Remaining quantity {line.Quantity} • Net {Money.Display(line.NetCents)}",
                quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
            quantities.Add((line.ChargeId,input));
            _detail.Add(input);
        }
        _detail.Add(status,Form.Action("Save credit note for review",status,async()=>
        {
            var value=draft with{Name=reason.Text,IssueDate=Input.Date(date.Text),
                Portions=quantities.Select(q=>new InvoicePortion(q.Charge,Input.Decimal(q.Input.Text))).Where(p=>p.Quantity!=0).ToArray()};
            var saved=await Task.Run(()=>_book.SaveDraft(owner,value));
            Refresh();
            Review(saved);
        }));
    }

    private void Review(CreditNoteDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title("Review credit note"));
        try{_detail.Add(QuoteView.Snapshot(_book.Preview(owner,draft)));}
        catch(Exception error){status.Text=error.Message;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to issue");
        _detail.Add(status,Form.Action("Edit / refresh this draft",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Issue saved credit note",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var issued=await Task.Run(()=>_book.Issue(owner,draft.Id,draft.Revision,password));
                Refresh();
                Show(issued);
            }));
    }

    private void Show(DocumentVersion version)
    {
        _detail.Children.Clear();
        _detail.Add(Form.Title(version.ToString()),QuoteView.Snapshot(version.Snapshot),PdfArtifactView.Build(store,owner,version.Id));
    }
}
