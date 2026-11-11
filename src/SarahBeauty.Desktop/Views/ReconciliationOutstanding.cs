using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReconciliationOutstanding(ReconciliationLine[] lines)
{
    private readonly Dictionary<string,OutstandingCash> _selected=[];
    private readonly List<(ReconciliationLine Line,CheckBox Selected,TextBox Reason,TextBox Evidence)> _fields=[];
    private readonly StackPanel _page=Form.Panel();
    private int _offset;

    internal UIElement Build()
    {
        if(lines.Length==0)return Form.Text("There are no unmatched cash remainders to support as outstanding.");
        var panel=Form.Panel();var status=Form.Text();
        panel.Add(Form.Text("Only select a real deposit in transit or issued payment that remains outstanding. Record its full remaining amount, explanation, and evidence. Post missing bank transactions in Money and match them before completing."),
            Form.Action("Previous outstanding cash items",status,()=>{Move(-25);return Task.CompletedTask;}),
            Form.Action("Next outstanding cash items",status,()=>{Move(25);return Task.CompletedTask;}),status,_page);
        Show();return panel;
    }

    private void Move(int change){Save();_offset=Math.Clamp(_offset+change,0,((lines.Length-1)/25)*25);Show();}

    private void Show()
    {
        _fields.Clear();_page.Children.Clear();
        _page.Add(Form.Text($"Outstanding candidates {_offset+1}–{Math.Min(_offset+25,lines.Length)} of {lines.Length}"));
        foreach(var line in lines.Skip(_offset).Take(25))
        {
            var old=_selected.GetValueOrDefault(line.Id);
            var selected=new CheckBox{Content=$"{(line.SignedCents>0?"Deposit in transit":"Outstanding payment")} • {Money.Display(line.RemainingCents)} • {line.Date:yyyy-MM-dd} • {line.Description} • {line.Id[..8]}",IsChecked=old is not null};
            var reason=Form.Input("Outstanding explanation • "+line.Id[..8],old?.Explanation??"");
            var evidence=Form.Input("Outstanding evidence • "+line.Id[..8],old?.EvidenceId??"");
            _fields.Add((line,selected,reason,evidence));_page.Add(selected,reason,evidence);
        }
    }

    private void Save()
    {
        foreach(var f in _fields)
            if(f.Selected.IsChecked==true)_selected[f.Line.Id]=new(f.Line.Id,f.Line.RemainingCents,f.Reason.Text,f.Evidence.Text);
            else _selected.Remove(f.Line.Id);
    }

    internal OutstandingCash[] Read(){Save();return _selected.Values.ToArray();}
}
