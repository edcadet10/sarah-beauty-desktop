using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BankMatchHistory
{
    internal static UIElement Build(BankMatchingSources data,DateOnly date,Dictionary<string,string> names)
    {
        var ids=data.BankLines.Select(b=>b.Id).ToHashSet();
        var groups=data.Approvals.ToDictionary(g=>g.Id);
        var history=data.History.Where(m=>ids.Contains(m.BankLineId) && m.Date<=date).ToArray();
        var panel=Form.Panel();var table=Form.Panel();var status=Form.Text();int offset=0;
        void Show()
        {
            table.Children.Clear();
            table.Add(Form.Text($"History rows: {history.Length} • Page {offset/100+1}"),
                ReportTable.Build(["Effective date","Action","Amount","Owner / approval time","Reason"],history.Skip(offset).Take(100).Select(m=>new[]{
                    m.Date.ToString("yyyy-MM-dd"),m.ReversesId.Length==0?"Match":"Unmatch",Money.Display(m.Cents),
                    names.GetValueOrDefault(groups[m.GroupId].ApprovedBy,"Former owner")+" • "+groups[m.GroupId].AtUtc,groups[m.GroupId].Draft.Reason})));
        }
        panel.Add(Form.Action("Previous match history",status,()=>{offset=Math.Max(0,offset-100);Show();return Task.CompletedTask;}),
            Form.Action("Next match history",status,()=>{offset=Math.Min(Math.Max(0,(history.Length-1)/100)*100,offset+100);Show();return Task.CompletedTask;}),status,table);
        Show();return new Microsoft.UI.Xaml.Controls.Expander{Header="Retained match and unmatch history",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
}
