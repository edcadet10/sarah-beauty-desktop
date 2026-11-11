using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReceiptView(BusinessStore store,OwnerSession owner)
{
    private readonly ReceiptBook _book=new(store);
    private readonly QuoteBook _documents=new(store);
    private readonly ListView _drafts=new(){MaxHeight=160};
    private readonly ListView _issued=new(){MaxHeight=180};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var payments=new FinanceBook(store).Posted(owner).Where(p=>p.Source.Kind is FinancialKind.Receipt or FinancialKind.AssetDisposal && p.Source.AmountCents>0).ToList();
        var selected=Form.Records("Approved customer payment",payments.Select(p=>(p.Id,p.Source.ToString())));
        panel.Add(Form.Title("Payment receipts"),selected,Form.Action("Prepare receipt",_status,()=>
        {
            if(selected.SelectedId().Length==0)throw new ArgumentException("Choose an approved customer payment.");
            Edit(new ReceiptDraft{PaymentId=selected.SelectedId(),IssueDate=DateOnly.FromDateTime(DateTime.Today)});
            return Task.CompletedTask;
        }),_status,Form.Text("Saved drafts"),_drafts,Form.Text("Issued receipts"),_issued,_detail);
        _drafts.SelectionChanged+=(_,_)=>{if(_drafts.SelectedItem is ReceiptDraft draft)Review(draft);};
        _issued.SelectionChanged+=(_,_)=>{if(_issued.SelectedItem is DocumentVersion version)Show(version);};
        Refresh();
        return panel;
    }

    private void Refresh()
    {
        _drafts.ItemsSource=_book.Drafts(owner).Where(d=>d.Status=="Draft").ToList();
        _issued.ItemsSource=_documents.Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.Receipt).ToList();
    }

    private void Edit(ReceiptDraft draft)
    {
        _detail.Children.Clear();
        var date=Form.Input("Issue date (YYYY-MM-DD)",draft.IssueDate?.ToString("yyyy-MM-dd")??"");
        var reference=Form.Input("Client-safe payment reference (optional; printed on the receipt)",draft.ClientReference);
        var status=Form.Text();
        _detail.Add(Form.Text("The receipt shows approved payment and balance history as of this date. Review the client-safe reference before issuing. Internal account details and evidence are excluded."),
            date,reference,status,Form.Action("Save / refresh receipt for review",status,async()=>
            {
                var value=draft with{Name="Payment receipt",IssueDate=Input.Date(date.Text),ClientReference=reference.Text};
                var saved=await Task.Run(()=>_book.SaveDraft(owner,value));
                Refresh();
                Review(saved);
            }));
    }

    private void Review(ReceiptDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        try{_detail.Add(QuoteView.Snapshot(_book.Preview(owner,draft)));}
        catch(Exception error){status.Text=error.Message;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to issue");
        _detail.Add(status,Form.Action("Edit / refresh this draft",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Issue saved payment receipt",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var receipt=await Task.Run(()=>_book.Issue(owner,draft.Id,draft.Revision,password));
                Refresh();
                Show(receipt);
            }));
    }

    private void Show(DocumentVersion version)
    {
        _detail.Children.Clear();
        _detail.Add(Form.Title(version.ToString()),QuoteView.Snapshot(version.Snapshot),PdfArtifactView.Build(store,owner,version.Id));
    }
}
