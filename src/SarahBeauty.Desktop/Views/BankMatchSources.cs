using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class BankMatchSources
{
    private readonly ComboBox _bank,_cash;
    private readonly TextBox _amount=Form.Input("Amount to match in USD");
    private readonly TextBox _bankSearch=Form.Input("Search bank transactions"),_cashSearch=Form.Input("Search cash journal entries");
    private readonly SourceChoice[] _bankChoices,_cashChoices;
    private readonly Dictionary<string,BankLine> _banks;
    private readonly Dictionary<string,CashMatchLine> _cashLines;

    internal BankMatchSources(List<BankLine> bank,List<CashMatchLine> cash,List<MatchPosition> bankPositions,List<MatchPosition> cashPositions)
    {
        _banks=bank.ToDictionary(b=>b.Id);_cashLines=cash.ToDictionary(c=>c.Id);
        _bankChoices=bankPositions.Where(p=>p.RemainingCents>0).Select(p=>new SourceChoice(p.Id,
            $"{_banks[p.Id].Date:yyyy-MM-dd} • {Money.Display(p.SignedCents)} • Remaining {Money.Display(p.RemainingCents)} • {_banks[p.Id].Description} • {p.Id[..8]}")).ToArray();
        _cashChoices=cashPositions.Where(p=>p.RemainingCents>0).Select(p=>new SourceChoice(p.Id,
            $"{_cashLines[p.Id].Date:yyyy-MM-dd} • {Money.Display(p.SignedCents)} • Remaining {Money.Display(p.RemainingCents)} • {_cashLines[p.Id].Description} • {p.Id[..8]}")).ToArray();
        _bank=new(){Header="Unmatched bank transaction",MinWidth=200};
        _cash=new(){Header="Unmatched cash journal entry",MinWidth=200};
        Filter(_bank,_bankChoices,"");Filter(_cash,_cashChoices,"");
        _bankSearch.TextChanged+=(_,_)=>Filter(_bank,_bankChoices,_bankSearch.Text);
        _cashSearch.TextChanged+=(_,_)=>Filter(_cash,_cashChoices,_cashSearch.Text);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Text("The lists show saved match balances. Prepared portions are checked together before approval. Search to find sources beyond the first 50 matches."),_bankSearch,_bank,_cashSearch,_cash,_amount);
        return panel;
    }

    internal MatchPortion Read()
    {
        var portion=new MatchPortion((_bank.SelectedItem as SourceChoice)?.Id??"",(_cash.SelectedItem as SourceChoice)?.Id??"",Money.Parse(_amount.Text));
        if(portion.BankLineId.Length==0 || portion.JournalLineId.Length==0 || portion.Cents<=0)throw new ArgumentException("Choose both source lines and a positive match amount.");
        return portion;
    }

    internal string Describe(MatchPortion p)=>$"{Money.Display(p.Cents)} • Bank: {_banks[p.BankLineId].Description} ({p.BankLineId[..8]}) • Cash: {_cashLines[p.JournalLineId].Description} ({p.JournalLineId[..8]})";

    private static void Filter(ComboBox control,SourceChoice[] choices,string query)
    {
        string selected=(control.SelectedItem as SourceChoice)?.Id??"";
        var matches=choices.Where(c=>c.Label.Contains(query,StringComparison.OrdinalIgnoreCase)).Take(50).ToList();
        var kept=choices.SingleOrDefault(c=>c.Id==selected);
        if(kept is not null && matches.All(c=>c.Id!=selected))matches.Add(kept);
        var items=new[]{new SourceChoice("","Choose a source")}.Concat(matches).ToArray();
        control.ItemsSource=items;control.SelectedItem=items.SingleOrDefault(c=>c.Id==selected)??items[0];
    }

    private sealed record SourceChoice(string Id,string Label){public override string ToString()=>Label;}
}
