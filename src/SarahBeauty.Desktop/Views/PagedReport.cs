using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal static class PagedReport
{
    internal static UIElement Build(string title,string[] columns,IEnumerable<string[]> values)
    {
        var rows=values.ToArray();var panel=Form.Panel();var table=Form.Panel();var status=Form.Text();int offset=0;
        void Show()
        {
            table.Children.Clear();
            table.Add(Form.Text($"Rows: {rows.Length} • Page {offset/100+1}"),ReportTable.Build(columns,rows.Skip(offset).Take(100)));
        }
        panel.Add(Form.Action("Previous "+title.ToLowerInvariant(),status,()=>{offset=Math.Max(0,offset-100);Show();return Task.CompletedTask;}),
            Form.Action("Next "+title.ToLowerInvariant(),status,()=>{offset=Math.Min(Math.Max(0,(rows.Length-1)/100)*100,offset+100);Show();return Task.CompletedTask;}),status,table);
        Show();return new Expander{Header=title,Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
}
