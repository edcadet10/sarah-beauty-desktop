using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class CreditView(BusinessStore store,OwnerSession owner)
{
    private readonly CreditBook _book=new(store);
    private readonly ListView _credits=new(){MaxHeight=180};
    private readonly ListView _drafts=new(){MaxHeight=160};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var action=Form.Select("Credit action",["Issue funded credit","Return unused credit to its receipt"],"Issue funded credit");
        panel.Add(Form.Title("Customer credits"),Form.Text("Reserve unused receipt value for a customer. Returning unused credit makes that value available on its original receipt again."),
            action,Form.Action("Prepare credit action",_status,()=>
            {
                Edit(new CreditDraft{Action=(string)action.SelectedItem=="Issue funded credit"?CreditAction.Issue:CreditAction.ReturnToReceipt,Date=DateOnly.FromDateTime(DateTime.Today)});
                return Task.CompletedTask;
            }),_status,Form.Text("Funded credits"),_credits,Form.Text("Saved drafts"),_drafts,_detail);
        _drafts.SelectionChanged+=(_,_)=>{if(_drafts.SelectedItem is CreditDraft draft)Review(draft);};
        _credits.SelectionChanged+=(_,_)=>{if(_credits.SelectedItem is CreditChoice choice)ShowCredit(choice.Credit);};
        Refresh();
        return panel;
    }

    private void Refresh()
    {
        DateOnly date=DateOnly.FromDateTime(DateTime.Today);
        _credits.ItemsSource=_book.Credits(owner).Select(c=>new CreditChoice(c,$"{c.CustomerName} • Available {Money.Display(_book.Available(owner,c.Id,date).Cents)} as of {date:yyyy-MM-dd} • {c.Id[..8]}")).ToList();
        _drafts.ItemsSource=_book.Drafts(owner).Where(d=>d.Status=="Draft").ToList();
    }

    private void Edit(CreditDraft draft)
    {
        _detail.Children.Clear();
        var reason=Form.Input("Explicit decision / reason",draft.Name);
        var date=Form.Input("Effective date",draft.Date?.ToString("yyyy-MM-dd")??"");
        var amount=Form.Input("Amount in USD",Money.Input(draft.Cents));
        var payment=Form.Records("Original approved receipt",new FinanceBook(store).Posted(owner).Where(p=>p.Source.Kind is FinancialKind.Receipt or FinancialKind.ReceiptClassification or FinancialKind.UnclassifiedReceipt).Select(p=>(p.Id,$"{p.Source} • {p.Id[..8]}")),draft.PaymentId);
        var credit=Form.Records("Customer credit",_book.Credits(owner).Select(c=>(c.Id,$"{c.CustomerName} • {c.Id[..8]}")),draft.CreditId);
        var status=Form.Text();
        _detail.Add(Form.Title(draft.Action==CreditAction.Issue?"Funded credit draft":"Return unused credit"),reason,date,amount);
        _detail.Add(draft.Action==CreditAction.Issue?payment:credit,status,Form.Action("Save for review",status,async()=>
        {
            var value=draft with{Name=reason.Text,Date=Input.Date(date.Text),Cents=Money.Parse(amount.Text),
                PaymentId=draft.Action==CreditAction.Issue?payment.SelectedId():"",CreditId=draft.Action==CreditAction.ReturnToReceipt?credit.SelectedId():""};
            var saved=await Task.Run(()=>_book.SaveDraft(owner,value));
            Refresh();
            Review(saved);
        }));
    }

    private void Review(CreditDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title("Review saved credit action"),Form.Text($"{draft.Action} • {draft.Date:yyyy-MM-dd} • {draft.Name}"));
        try
        {
            var preview=_book.Preview(owner,draft);
            _detail.Add(Form.Text($"{preview.CustomerName} • {preview.SourceTreatment} • Amount {Money.Display(preview.Cents)} including tax {Money.Display(preview.TaxCents)}\n"+
                $"Receipt available afterward: {Money.Display(preview.ReceiptAfter.Cents)}\nCredit available afterward: {Money.Display(preview.CreditAfter.Cents)}"));
        }
        catch(Exception error){status.Text=error.Message;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to approve");
        _detail.Add(status,Form.Action("Edit / refresh available value",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Approve credit action",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var credit=await Task.Run(()=>_book.Approve(owner,draft.Id,draft.Revision,password));
                Refresh();
                ShowCredit(credit);
            }));
    }

    private void ShowCredit(CustomerCredit credit)
    {
        _detail.Children.Clear();
        DateOnly date=DateOnly.FromDateTime(DateTime.Today);
        var available=_book.Available(owner,credit.Id,date);
        _detail.Add(Form.Title(credit.CustomerName),Form.Text($"Issued {Money.Display(credit.IssuedCents)} on {credit.IssueDate:yyyy-MM-dd}\n"+
            $"Available as of {date:yyyy-MM-dd}: {Money.Display(available.Cents)} including tax {Money.Display(available.TaxCents)}"),
            Form.Text($"Original receipt reference: {credit.PaymentId[..8]}"),
            Form.Text(credit.Reason),Form.Text($"Source treatment: {credit.SourceTreatment}. Issuing credit does not change that classification."),
            Form.Text(credit.SourceTreatment=="Recognized income"?"Use Apply payments to allocate this credit to an invoice or reverse an earlier allocation.":
                "Held or suspense credit needs an explicit return to receipt availability and reviewed reclassification before invoice use. Refunding unused credit retains its original source classification."));
    }

    private sealed record CreditChoice(CustomerCredit Credit,string Label){public override string ToString()=>Label;}
}
