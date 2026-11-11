using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class CancellationValueFields
{
    private readonly SearchableRecords _source=new("Cancellation receipt or funded credit"),_destination=new("Destination booking for transferred credit");
    private readonly TextBox _retain,_refund,_tax,_transfer,_method,_evidence;
    private readonly ComboBox _cash;
    private readonly CheckBox _resolved=new(){Content="I reviewed the refund tax treatment",MinHeight=44};
    private readonly TaxRefundFields _taxSplit;

    internal CancellationValueFields(CancellationSnapshot sources,List<Account> accounts,List<Booking> bookings,CancellationValueChoice value)
    {
        var payments=sources.Payments.Select(p=>(p.Id+"/",$"{p.Kind} • {p.CustomerName} • {p.Date:yyyy-MM-dd} • Available {Money.Display(p.Available.Cents)} • {p.Classification} • {p.Id[..8]}"));
        var credits=sources.Credits.Select(c=>(c.Source.PaymentId+"/"+c.Source.Id,$"Funded credit • {c.Source.CustomerName} • Available {Money.Display(c.Available.Cents)} • {c.Source.SourceTreatment} • {c.Source.Id[..8]}"));
        _source.Update(payments.Concat(credits),value.PaymentId+"/"+value.CreditId);
        _destination.Update(bookings.Where(b=>b.Id!=sources.BookingId && b.Status!="Cancelled" && !b.Archived).Select(b=>(b.Id,b.ToString()+" • "+b.Id[..8])),value.DestinationBookingId);
        _retain=Form.Input("Retain existing treatment in USD",Money.Input(value.RetainCents));_refund=Form.Input("Actual cancellation refund in USD",Money.Input(value.RefundCents));
        _tax=Form.Input("Included cancellation refund tax in USD",Money.Input(value.RefundTaxCents));_transfer=Form.Input("Transfer as funded credit in USD",Money.Input(value.TransferCents));
        _cash=Form.Records("Actual cancellation refund account",accounts.Where(a=>a.Active && a.CashKind!="None").Select(a=>(a.Id,a.ToString())),value.RefundCashAccountId);
        _method=Form.Input("Actual cancellation refund method",value.RefundMethod);_evidence=Form.Input("Actual cancellation refund evidence",value.RefundEvidenceId);
        _resolved.IsChecked=value.RefundTaxResolved;
        _taxSplit=new(new FinancialDraft{Kind=FinancialKind.Refund,TaxCents=value.RefundTaxCents,TaxRefundAllocation=value.TaxRefundAllocation},_cash);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(_source.Build(),_retain,_refund,_tax,_resolved,_cash,_method,_evidence,_taxSplit.Build(),_transfer,_destination.Build(),
            Form.Text("Retained value keeps its existing income, held, or suspense treatment. Transfer reserves credit without another receipt or invoice application. Leave the optional destination undecided until agreed."));return panel;
    }

    internal CancellationValueChoice Read()
    {
        string[] ids=_source.SelectedId.Split('/');
        if(ids.Length!=2 || ids[0].Length==0)throw new ArgumentException("Choose the actual receipt or funded credit for this value decision.");
        var split=_taxSplit.Read(new FinancialDraft{Kind=FinancialKind.Refund}).TaxRefundAllocation;
        return new(ids[0],ids[1],Money.Parse(_retain.Text),Money.Parse(_refund.Text),Money.Parse(_tax.Text),Money.Parse(_transfer.Text),
            _destination.SelectedId,_cash.SelectedId(),_method.Text.Trim(),_evidence.Text.Trim(),split,_resolved.IsChecked==true);
    }
}
