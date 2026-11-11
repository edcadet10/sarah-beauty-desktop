using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class PurchaseView(BusinessStore store,OwnerSession owner)
{
    private readonly PurchaseBook _book=new(store);
    private readonly ListView _list=new(){MaxHeight=220};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        new ChartOfAccounts(store).Initialize(owner);
        var panel=Form.Panel();
        panel.Add(Form.Title("Supplier bills"),Form.Text("Prepare and review the supplier bill separately from its actual payment. An unpaid approved bill is an operational commitment; it does not post a cash-method expense."),
            Form.Action("New supplier bill",_status,()=>{Edit(new Purchase{PurchaseDate=DateOnly.FromDateTime(DateTime.Today)});return Task.CompletedTask;}),_status,_list,_detail);
        _list.SelectionChanged+=(_,_)=>{try{if(_list.SelectedItem is Purchase value)Review(value);}catch(Exception error){_status.Text=error.Message;}};
        Refresh();
        return panel;
    }

    private void Refresh()=>_list.ItemsSource=_book.List(owner);

    private void Edit(Purchase value)
    {
        _detail.Children.Clear();
        var fields=new PurchaseFields(store,owner,value);
        var status=Form.Text();
        _detail.Add(fields.Build(),status,Form.Action("Save bill for review",status,async()=>
        {
            var draft=fields.Read(value);
            var saved=await Task.Run(()=>_book.SaveDraft(owner,draft));
            Refresh();
            Review(saved);
        }));
    }

    private void Review(Purchase value)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title(value.Status=="Approved"?"Approved supplier bill":"Review supplier bill"),Form.Text(value.ToString()),
            Form.Text(value.EvidenceId.Length==0?"Bill evidence missing":"Evidence reference: "+value.EvidenceId));
        try
        {
            if(value.Status=="Draft")_book.Preview(owner,value);
            long gross=value.Lines.Sum(l=>l.GrossCents!.Value),business=value.Lines.Sum(l=>l.BusinessCents!.Value);
            _detail.Add(Form.Text(string.Join("\n",value.Lines.Select(l=>l.ToString()))),
                Form.Text($"Supplier total {Money.Display(gross)} • Business share {Money.Display(business)} • Personal share {Money.Display(gross-business)}"));
        }
        catch(Exception error){status.Text=error.Message;}
        _detail.Add(status);
        if(value.Status!="Draft")
        {
            var today=DateOnly.FromDateTime(DateTime.Today);
            var due=new PurchasePayments(store).Outstanding(owner,today).SingleOrDefault(p=>p.PurchaseId==value.Id);
            string balance=due is null?"This bill is outside today's balances.":$"As of {today:yyyy-MM-dd} • Unpaid {Money.Display(due.PayableCents)} • Supplier credit {Money.Display(due.CreditCents)}";
            _detail.Add(Form.Text(balance),
                Form.Text("Prepare the actual PurchasePayment movement in Money and choose this approved bill. Payment is reviewed and approved separately."));
            return;
        }
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to approve the bill");
        _detail.Add(Form.Action("Edit bill draft",status,()=>{Edit(value);return Task.CompletedTask;}),phrase,
            Form.Action("Approve saved supplier bill",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var saved=await Task.Run(()=>_book.Approve(owner,value.Id,value.Revision,password));
                Refresh();
                Review(saved);
            }));
    }
}
