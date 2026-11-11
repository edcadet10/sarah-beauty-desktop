using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class CancellationApplications(CancellationSnapshot sources,CancellationUnapply[] initial)
{
    private readonly List<Row> _rows=[];
    private readonly StackPanel _panel=Form.Panel();

    internal UIElement Build()
    {
        var root=Form.Panel();var status=Form.Text();
        foreach(var value in initial)Add(value);
        root.Add(Form.Text("Select only the original applications to reverse. A refund alone does not undo an invoice application."),_panel,
            Form.Action("Add cancellation unapplication",status,()=>{Add(new("",0));return Task.CompletedTask;}),status);
        return root;
    }

    internal CancellationUnapply[] Read()=>_rows.Select(r=>r.Read()).ToArray();

    private void Add(CancellationUnapply value)
    {
        var row=new Row(sources,value);var container=Form.Panel();var status=Form.Text();_rows.Add(row);
        container.Add(row.Build(),Form.Action("Remove this cancellation unapplication",status,()=>{_rows.Remove(row);_panel.Children.Remove(container);return Task.CompletedTask;}),status);
        _panel.Add(container);
    }

    private sealed class Row(CancellationSnapshot sources,CancellationUnapply value)
    {
        private readonly SearchableRecords _source=new("Original cancellation application");
        private readonly TextBox _amount=Form.Input("Application amount to reverse in USD",Money.Input(value.Cents));
        internal UIElement Build()
        {
            _source.Update(sources.Applications.Where(a=>a.InvoiceBookingId==sources.BookingId).Select(a=>(a.Id,
                $"{a.InvoiceNumber} • {a.Date:yyyy-MM-dd} • Remaining {Money.Display(a.Remaining.Cents)} • After later reversals {Money.Display(a.RemainingAfterAllDatedActions.Cents)} • {a.Id[..8]}")),value.ApplicationId);
            var panel=Form.Panel();panel.Add(_source.Build(),_amount);return panel;
        }
        internal CancellationUnapply Read()=>new(_source.SelectedId,Money.Parse(_amount.Text));
    }
}
