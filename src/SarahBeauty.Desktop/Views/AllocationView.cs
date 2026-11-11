using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class AllocationView(BusinessStore store,OwnerSession owner)
{
    private readonly PaymentApplications _book=new(store);
    private readonly StackPanel _detail=Form.Panel();
    private readonly ListView _drafts=new(){MaxHeight=170};
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Apply payments"),Form.Text("Match approved receipts to payer invoices. Applications allocate received value; the original receipt remains in the books."),
            Form.Action("New payment application",_status,()=>
            {Edit(new PaymentApplicationDraft{Name="Payment application",Date=DateOnly.FromDateTime(DateTime.Today)});return Task.CompletedTask;}),
            _status,_drafts,_detail);
        _drafts.SelectionChanged+=(_,_)=>{if(_drafts.SelectedItem is PaymentApplicationDraft draft)Review(draft);};
        Refresh();
        return panel;
    }

    private void Refresh()=>_drafts.ItemsSource=_book.Drafts(owner).Where(d=>d.Status=="Draft").ToList();

    private void Edit(PaymentApplicationDraft draft)
    {
        _detail.Children.Clear();
        var fields=new AllocationFields(store,owner,draft);
        var status=Form.Text();
        _detail.Add(Form.Title(draft.ReversesId.Length>0?"Reverse an application":"Application draft"),fields.Build(),status,
            Form.Action("Save for review",status,async()=>
            {
                var value=fields.Read(draft);
                var saved=await Task.Run(()=>_book.SaveDraft(owner,value));
                Refresh();
                Review(saved);
            }));
    }

    private void Review(PaymentApplicationDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title("Review saved application"),Form.Text($"{draft.Name} • {draft.Date:yyyy-MM-dd} • {Money.Display(draft.Cents)}"));
        try
        {
            var dues=_book.Outstanding(owner,draft.Date??DateOnly.FromDateTime(DateTime.Today));
            _detail.Add(Form.Text(string.Join("\n",_book.Preview(owner,draft).Select(a=>
            {
                var due=dues.FirstOrDefault(d=>d.MilestoneId==a.MilestoneId);
                return $"{due?.Number} • {due?.Kind} • {Money.Display(a.Cents)} including tax {Money.Display(a.TaxCents)}";
            }))));
        }
        catch(Exception error){status.Text=error.Message;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to approve");
        _detail.Add(status,Form.Action("Edit / refresh balances",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Approve this application",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                await Task.Run(()=>_book.Approve(owner,draft.Id,draft.Revision,password));
                Refresh();
                _detail.Children.Clear();
                _detail.Add(Form.Text("Application recorded. Receipt availability and invoice balances were updated together."));
            }));
    }
}
