using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class InvoiceFields
{
    private readonly TextBox _name,_date,_due,_terms;
    private readonly ComboBox _payer;
    private readonly List<(string ChargeId,TextBox Quantity)> _quantities=[];
    private readonly StackPanel _panel=Form.Panel();

    internal InvoiceFields(BusinessStore store,OwnerSession owner,InvoiceDraft draft)
    {
        _name=Form.Input("Draft name",draft.Name);
        _date=Form.Input("Invoice issue date",draft.IssueDate?.ToString("yyyy-MM-dd")??"");
        _due=Form.Input("Invoice due date (optional)",draft.DueDate?.ToString("yyyy-MM-dd")??"");
        _payer=Form.Records("Payer for this invoice",new CustomerBook(store).Search(owner,includeArchived:true).Select(c=>(c.Id,$"{c.FullName} • {c.Id[..8]}")),draft.PayerCustomerId);
        _terms=Form.MultilineInput("Terms for this invoice",draft.Terms);
        _panel.Add(_name,_date,_due,_payer,_terms,Form.Text("Choose the source quantities this payer will cover. Enter zero to leave a charge for another payer. The booking's trial and deposit are shared once across all invoices."));
        foreach(var remaining in new InvoiceBook(store).Remaining(owner,draft.QuoteVersionId))
        {
            decimal selected=draft.Portions.SingleOrDefault(p=>p.ChargeId==remaining.ChargeId)?.Quantity??0;
            var quantity=Form.Input($"{remaining.Description} • Unbilled quantity {remaining.Quantity} • Net {Money.Display(remaining.NetCents)}",selected.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _quantities.Add((remaining.ChargeId,quantity));
            _panel.Add(quantity);
        }
    }

    internal UIElement Build()=>_panel;
    internal InvoiceDraft Read(InvoiceDraft previous)=>previous with
    {
        Name=_name.Text,IssueDate=Input.Date(_date.Text),DueDate=Input.Date(_due.Text),PayerCustomerId=_payer.SelectedId(),Terms=_terms.Text,
        Portions=_quantities.Select(q=>new InvoicePortion(q.ChargeId,Input.Decimal(q.Quantity.Text))).Where(p=>p.Quantity!=0).ToArray()
    };
}
