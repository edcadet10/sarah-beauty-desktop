using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class PurchaseCreditView(BusinessStore store,OwnerSession owner)
{
    private readonly PurchaseCreditBook _book=new(store);
    private readonly ListView _list=new(){MaxHeight=220};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var bills=new PurchaseBook(store).List(owner).Where(p=>p.Status=="Approved").ToList();
        var select=Form.Records("Original supplier bill",bills.Select(b=>(b.Id,b.ToString())));
        panel.Add(Form.Title("Supplier bill credits"),Form.Text("Record the supplier's reduction separately from cash refunded. An unpaid reduction creates no money movement. A paid reduction shows credit owed by the supplier until refund or later use."),
            select,Form.Action("Prepare supplier credit",_status,()=>
            {
                if(select.SelectedId().Length==0)throw new ArgumentException("Choose the original approved bill.");
                Edit(new PurchaseCredit{PurchaseId=select.SelectedId(),Date=DateOnly.FromDateTime(DateTime.Today)});
                return Task.CompletedTask;
            }),_status,_list,_detail);
        _list.SelectionChanged+=(_,_)=>{try{if(_list.SelectedItem is PurchaseCredit value)Review(value);}catch(Exception error){_status.Text=error.Message;}};
        Refresh();
        return panel;
    }

    private void Refresh()=>_list.ItemsSource=_book.List(owner);

    private void Edit(PurchaseCredit value)
    {
        _detail.Children.Clear();
        var fields=new PurchaseCreditFields(value,_book.Remaining(owner,value.PurchaseId));
        var status=Form.Text();
        _detail.Add(fields.Build(),status,Form.Action("Save supplier credit for review",status,async()=>
        {
            var draft=fields.Read(value);
            var saved=await Task.Run(()=>_book.SaveDraft(owner,draft));
            Refresh();
            Review(saved);
        }));
    }

    private void Review(PurchaseCredit value)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title(value.Sign<0?"Supplier credit reversal":"Supplier bill reduction"),Form.Text(value.ToString()));
        try
        {
            var review=value.Status=="Draft"?_book.Preview(owner,value):value;
            _detail.Add(Form.Text($"Gross {Money.Display(review.Lines.Sum(l=>l.Cents))} • Included supplier tax {Money.Display(review.Lines.Sum(l=>l.TaxCents!.Value))} • Business share {Money.Display(review.Lines.Sum(l=>l.BusinessCents))}"),
                Form.Text(review.EvidenceId.Length==0?"Supplier evidence missing":"Evidence reference: "+review.EvidenceId));
        }
        catch(Exception error){status.Text=error.Message;}
        _detail.Add(status);
        if(value.Status=="Approved")
        {
            if(value.Sign==1)_detail.Add(Form.Action("Prepare a linked reversal",status,()=>{Edit(_book.ReversalDraft(owner,value.Id));return Task.CompletedTask;}));
            return;
        }
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase");
        _detail.Add(Form.Action("Edit / refresh draft",status,()=>{Edit(value);return Task.CompletedTask;}),phrase,
            Form.Action("Approve supplier adjustment",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var saved=await Task.Run(()=>_book.Approve(owner,value.Id,value.Revision,password));
                Refresh();
                Review(saved);
            }));
    }
}
