using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class StatementPreviewView
{
    private readonly TextBox _csv=new(){Header="CSV contents (UTF-8; header required)",AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,Height=180};
    private readonly StatementCsvFields _fields=new();
    private readonly StackPanel _preview=Form.Panel();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var status=Form.Text();
        panel.Add(Form.Title("Statement preview"),Form.Text("Open or paste a USD statement export. Review the exact date format, signs, and source rows. This preview does not save bank lines or change the books. Statement import and matching are being added separately."),
            Form.Action("Choose statement CSV",status,async()=>{string? text=await StatementFilePicker.ReadCsv();if(text is not null)_csv.Text=text;}),
            _csv,_fields.Build(),status,Form.Action("Preview statement rows",status,async()=>
            {
                string contents=_csv.Text;
                var mapping=_fields.Read();
                var source=await Task.Run(()=>StatementCsv.Parse(contents,mapping));
                Show(source);
            }),_preview);
        return panel;
    }

    private void Show(StatementSource source)
    {
        _preview.Children.Clear();
        int repeated=source.Rows.GroupBy(r=>(r.Date,r.SignedCents,r.Description,r.Reference)).Where(g=>g.Count()>1).Sum(g=>g.Count());
        _preview.Add(Form.Text($"Rows: {source.Rows.Length} • Money in: {Money.Display(source.InflowCents)} • Money out: {Money.Display(source.OutflowCents)} • Net change: {Money.Display(source.NetCents)}"),
            Form.Text($"Identical source rows: {repeated}. Each remains separate for review. Equal dates and amounts do not prove a duplicate."),
            ReportTable.Build(["Source row","Date","Signed USD","Description","Reference"],source.Rows.Take(250).Select(r=>new[]{r.SourceRow.ToString(),r.Date.ToString("yyyy-MM-dd"),Money.Display(r.SignedCents),r.Description,r.Reference})));
        if(source.Rows.Length>250)_preview.Add(Form.Text("Showing the first 250 rows. The totals include all parsed rows."));
    }
}
