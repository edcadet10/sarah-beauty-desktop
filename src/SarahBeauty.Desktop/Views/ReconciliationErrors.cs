using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReconciliationErrors(ReconciliationSources sources)
{
    private readonly SearchableRecords _bank=new("Bank transaction containing an error");
    private readonly TextBox _reason=Form.Input("Bank-error explanation"),_evidence=Form.Input("Bank-error evidence");
    private readonly ListView _list=new(){MaxHeight=200};
    private readonly Dictionary<string,StatementError> _errors=[];
    internal event Action? Changed;

    internal UIElement Build()
    {
        var panel=Form.Panel();var status=Form.Text();
        _bank.Update(sources.Bank.Where(b=>b.RemainingCents>0).Select(b=>(b.Id,$"{b.Date:yyyy-MM-dd} • {Money.Display(b.SignedCents)} • Unmatched {Money.Display(b.RemainingCents)} • {b.Description} • {b.Id[..8]}")));
        panel.Add(Form.Title("Document an actual bank-statement error"),
            Form.Text("Use bank evidence for an incorrect statement amount. This claims the full unmatched remainder of that row. Record a missing business transaction in Money instead. Removing an error also removes its prepared corrections. Search lists show up to 50 matches."),
            _bank.Build(),_reason,_evidence,status,Form.Action("Add or update statement error",status,()=>{Add();return Task.CompletedTask;}),_list,
            Form.Action("Remove selected statement error",status,()=>{Remove();return Task.CompletedTask;}));
        return panel;
    }

    private void Add()
    {
        var line=sources.Bank.SingleOrDefault(b=>b.Id==_bank.SelectedId)??throw new ArgumentException("Choose an unmatched bank transaction.");
        if(string.IsNullOrWhiteSpace(_reason.Text) || string.IsNullOrWhiteSpace(_evidence.Text))throw new ArgumentException("Enter the bank-error explanation and evidence.");
        string id=_errors.GetValueOrDefault(line.Id)?.Id??Guid.NewGuid().ToString();
        _errors[line.Id]=new(id,line.Id,line.RemainingCents,_reason.Text,_evidence.Text);Refresh();
    }

    private void Remove()
    {
        if(_list.SelectedItem is not ErrorItem selected)throw new ArgumentException("Select a prepared statement error to remove.");
        _errors.Remove(selected.Error.BankLineId);Refresh();
    }

    private void Refresh()
    {
        _list.ItemsSource=_errors.Values.Select(e=>new ErrorItem(e,$"{Money.Display(e.Cents)} • {e.Explanation} • {e.EvidenceId} • {e.BankLineId[..8]}")).ToArray();Changed?.Invoke();
    }

    internal StatementError[] Read()=>_errors.Values.ToArray();
    private sealed record ErrorItem(StatementError Error,string Label){public override string ToString()=>Label;}
}
