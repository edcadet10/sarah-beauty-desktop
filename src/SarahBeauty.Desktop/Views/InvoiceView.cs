using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class InvoiceView(BusinessStore store,OwnerSession owner)
{
    private readonly InvoiceBook _invoices=new(store);
    private readonly QuoteBook _documents=new(store);
    private readonly ListView _drafts=new(){MaxHeight=160};
    private readonly ListView _issued=new(){MaxHeight=180};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var quotes=_documents.Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.Quote && v.Status=="Accepted").ToList();
        var select=Form.Records("Accepted quote",quotes.Select(q=>(q.Id,q.ToString())));
        panel.Add(Form.Title("Invoices"),select,Form.Action("Prepare payer invoice",_status,()=>
        {
            var quote=quotes.SingleOrDefault(q=>q.Id==select.SelectedId())??throw new ArgumentException("Choose an accepted quote.");
            Edit(new InvoiceDraft{Name=quote.Snapshot.Event.Name+" invoice",QuoteVersionId=quote.Id,
                IssueDate=DateOnly.FromDateTime(DateTime.Today),Terms=quote.Snapshot.Terms,
                Portions=_invoices.Remaining(owner,quote.Id).Where(r=>r.Quantity>0).Select(r=>new InvoicePortion(r.ChargeId,r.Quantity)).ToArray()});
            return Task.CompletedTask;
        }),_status,Form.Text("Saved invoice drafts"),_drafts,Form.Text("Issued invoices"),_issued,_detail);
        _drafts.SelectionChanged+=(_,_)=>{if(_drafts.SelectedItem is InvoiceDraft draft)Review(draft);};
        _issued.SelectionChanged+=(_,_)=>{if(_issued.SelectedItem is DocumentVersion version)ShowIssued(version);};
        Refresh();
        return panel;
    }

    private void Refresh()
    {
        _drafts.ItemsSource=_invoices.Drafts(owner).Where(d=>d.Status=="Draft").ToList();
        _issued.ItemsSource=_documents.Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.Invoice).ToList();
    }

    private void Edit(InvoiceDraft draft)
    {
        _detail.Children.Clear();
        var fields=new InvoiceFields(store,owner,draft);
        var status=Form.Text();
        _detail.Add(Form.Title("Payer invoice draft"),fields.Build(),status,
            Form.Action("Save invoice for review",status,async()=>
            {
                var value=fields.Read(draft);
                var saved=await Task.Run(()=>_invoices.SaveDraft(owner,value));
                Refresh();
                Review(saved);
            }));
    }

    private void Review(InvoiceDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title("Review saved payer invoice"));
        try{_detail.Add(QuoteView.Snapshot(_invoices.Preview(owner,draft)));}
        catch(Exception error){status.Text=error.Message;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to issue");
        _detail.Add(status,Form.Action("Edit / refresh this invoice draft",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Issue saved payer invoice",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var invoice=await Task.Run(()=>_invoices.Issue(owner,draft.Id,draft.Revision,password));
                Refresh();
                ShowIssued(invoice);
            }));
    }

    private void ShowIssued(DocumentVersion version)
    {
        _detail.Children.Clear();
        _detail.Add(Form.Title(version.ToString()),QuoteView.Snapshot(version.Snapshot),PdfArtifactView.Build(store,owner,version.Id),Form.Text("Open Document decisions to review retained history or void an unused invoice. Invoices with payment or credit-note history require linked adjustments."));
    }
}
