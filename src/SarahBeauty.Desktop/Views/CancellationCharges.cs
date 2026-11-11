using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class CancellationCharges(CancellationSnapshot sources,CancellationReduction[] initial)
{
    private readonly List<Row> _rows=[];
    private readonly StackPanel _panel=Form.Panel();
    internal UIElement Build()
    {
        var root=Form.Panel();var status=Form.Text();
        foreach(var group in initial)foreach(var portion in group.Portions)Add(group.InvoiceVersionId,portion);
        root.Add(Form.Text("Select original invoiced quantities to reduce. Unapply affected paid amounts first; retained charges stay due unless paid."),_panel,
            Form.Action("Add cancellation charge reduction",status,()=>{Add("",new("",1));return Task.CompletedTask;}),status);return root;
    }
    internal CancellationReduction[] Read()=>_rows.Select(r=>r.Read()).GroupBy(r=>r.Invoice).Select(g=>new CancellationReduction(g.Key,g.Select(r=>r.Portion).ToArray())).ToArray();
    private void Add(string invoice,InvoicePortion portion)
    {
        var row=new Row(sources,invoice,portion);var container=Form.Panel();var status=Form.Text();_rows.Add(row);
        container.Add(row.Build(),Form.Action("Remove this cancellation charge reduction",status,()=>{_rows.Remove(row);_panel.Children.Remove(container);return Task.CompletedTask;}),status);_panel.Add(container);
    }
    private sealed class Row(CancellationSnapshot sources,string invoice,InvoicePortion portion)
    {
        private readonly SearchableRecords _source=new("Original cancellation invoice charge");
        private readonly TextBox _quantity=Form.Input("Cancellation quantity to reduce",portion.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        internal UIElement Build()
        {
            _source.Update(sources.Charges.Select(c=>(c.InvoiceVersionId+"/"+c.Source.ChargeId,
                $"{sources.Documents.Single(d=>d.Id==c.InvoiceVersionId).Snapshot.Number} • {c.Source.Description} • Remaining quantity {c.RemainingQuantity} • After later reductions {c.RemainingQuantityAfterAllDatedActions}")),invoice+"/"+portion.ChargeId);
            var panel=Form.Panel();panel.Add(_source.Build(),_quantity);return panel;
        }
        internal (string Invoice,InvoicePortion Portion) Read()
        {
            var chosen=sources.Charges.SingleOrDefault(c=>c.InvoiceVersionId+"/"+c.Source.ChargeId==_source.SelectedId)??throw new ArgumentException("Choose an original invoice charge to reduce.");
            return(chosen.InvoiceVersionId,new(chosen.Source.ChargeId,Input.Decimal(_quantity.Text)));
        }
    }
}
