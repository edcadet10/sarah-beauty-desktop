using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class BankUnmatchFields
{
    private readonly ComboBox _original;
    private readonly TextBox _amount=Form.Input("Amount to unmatch in USD");
    private readonly Dictionary<string,BankMatch> _matches;

    internal BankUnmatchFields(List<BankMatch> history,List<BankLine> bank,List<CashMatchLine> cash,DateOnly date)
    {
        var banks=bank.ToDictionary(b=>b.Id);var cashLines=cash.ToDictionary(c=>c.Id);
        _matches=history.Where(m=>m.ReversesId.Length==0 && m.Date<=date && banks.ContainsKey(m.BankLineId)).ToDictionary(m=>m.Id);
        var choices=_matches.Values.Select(m=>(Match:m,Left:m.Cents-history.Where(r=>r.ReversesId==m.Id && r.Date<=date).Sum(r=>r.Cents))).Where(p=>p.Left>0);
        _original=Form.Records("Original match to undo",choices.Select(p=>(p.Match.Id,$"{p.Match.Date:yyyy-MM-dd} • {Money.Display(p.Left)} still matched • {banks[p.Match.BankLineId].Description} ↔ {cashLines[p.Match.JournalLineId].Description} • {p.Match.Id[..8]}")));
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(_original,_amount);return panel;
    }

    internal MatchPortion Read()
    {
        if(!_matches.TryGetValue(_original.SelectedId(),out var original))throw new ArgumentException("Choose the original active match to undo.");
        long amount=Money.Parse(_amount.Text);Money.Positive(amount);
        return new(original.BankLineId,original.JournalLineId,amount,original.Id);
    }
}
