using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReconciliationErrorCorrections(ReconciliationSources sources,Func<StatementError[]> newErrors)
{
    private readonly SearchableRecords _error=new("Statement error being corrected"),_bank=new("Actual bank correction transaction");
    private readonly TextBox _amount=Form.Input("Actual bank correction amount in USD"),_reason=Form.Input("Bank correction explanation"),_evidence=Form.Input("Bank correction evidence");
    private readonly ListView _list=new(){MaxHeight=200};
    private readonly Dictionary<(string Error,string Bank),StatementErrorResolution> _corrections=[];

    internal UIElement Build()
    {
        var panel=Form.Panel();var status=Form.Text();
        _bank.Update(sources.Bank.Where(b=>b.RemainingCents>0).Select(b=>(b.Id,$"{b.Date:yyyy-MM-dd} • {Money.Display(b.SignedCents)} • Unmatched {Money.Display(b.RemainingCents)} • {b.Description} • {b.Id[..8]}")));
        RefreshErrors();
        panel.Add(Form.Title("Link actual bank corrections"),Form.Text("Choose the original error and the bank's actual opposite-direction correction. A correction can be partial. Unresolved amounts carry into later statements; no cash or income is posted."),
            _error.Build(),_bank.Build(),_amount,_reason,_evidence,status,
            Form.Action("Add or update bank correction",status,()=>{Add();return Task.CompletedTask;}),_list,
            Form.Action("Remove selected bank correction",status,()=>{Remove();return Task.CompletedTask;}));
        return panel;
    }

    internal void RefreshErrors()
    {
        var choices=sources.CarriedErrors.Select(e=>(e.Id,$"Carried • Remaining {Money.Display(e.RemainingCents)} • {e.Explanation} • {e.Id[..8]}"))
            .Concat(newErrors().Select(e=>(e.Id,$"New • {Money.Display(e.Cents)} • {e.Explanation} • {e.Id[..8]}"))).ToArray();
        var allowed=choices.Select(c=>c.Id).ToHashSet();
        foreach(var key in _corrections.Keys.Where(k=>!allowed.Contains(k.Error)).ToArray())_corrections.Remove(key);
        _error.Update(choices);Refresh();
    }

    private void Add()
    {
        long cents=Money.Parse(_amount.Text);
        if(_error.SelectedId.Length==0 || _bank.SelectedId.Length==0 || cents<=0 || string.IsNullOrWhiteSpace(_reason.Text) || string.IsNullOrWhiteSpace(_evidence.Text))
            throw new ArgumentException("Choose both sources and enter a positive correction amount, explanation, and evidence.");
        var correction=new StatementErrorResolution(_error.SelectedId,_bank.SelectedId,cents,_reason.Text,_evidence.Text);
        _corrections[(correction.ErrorId,correction.BankLineId)]=correction;Refresh();
    }

    private void Remove()
    {
        if(_list.SelectedItem is not CorrectionItem item)throw new ArgumentException("Select a prepared bank correction to remove.");
        _corrections.Remove((item.Value.ErrorId,item.Value.BankLineId));Refresh();
    }

    private void Refresh()=>_list.ItemsSource=_corrections.Values.Select(c=>new CorrectionItem(c,$"{Money.Display(c.Cents)} • {c.Explanation} • {c.EvidenceId} • Error {c.ErrorId[..8]} → bank {c.BankLineId[..8]}")).ToArray();
    internal StatementErrorResolution[] Read()=>_corrections.Values.ToArray();
    private sealed record CorrectionItem(StatementErrorResolution Value,string Label){public override string ToString()=>Label;}
}
