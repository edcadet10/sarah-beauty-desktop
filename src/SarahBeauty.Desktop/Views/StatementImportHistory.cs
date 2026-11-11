using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class StatementImportHistory
{
    internal static UIElement Build(StatementImport statement,List<StatementImportLine> rows,string account,string actor)
    {
        var panel=Form.Panel();
        var table=Form.Panel();
        var status=Form.Text();
        int offset=0;
        void Show()
        {
            table.Children.Clear();
            table.Add(Form.Text(rows.Count==0?"No transaction rows. This statement was confirmed as having no activity.":$"Showing source rows {offset+1}–{Math.Min(offset+100,rows.Count)} of {rows.Count}"),
                ReportTable.Build(["Source row","Date","USD","Original source description / reference","Decision / record code"],rows.Skip(offset).Take(100).Select(r=>new[]{
                    r.SourceRow.ToString(),r.Source.Date.ToString("yyyy-MM-dd"),Money.Display(r.Source.SignedCents),r.Source.Description+" • "+r.Source.Reference,r.Reason+" • "+r.Line.Id[..8]})));
        }
        panel.Add(Form.Title("Imported statement"),Form.Text($"{account} • {statement.From:yyyy-MM-dd} – {statement.Through:yyyy-MM-dd}\nOpening {Money.Display(statement.OpeningCents)} • Closing {Money.Display(statement.ClosingCents)}\nReviewed by {actor} at {statement.AtUtc}"),
            Form.Action("Previous imported source rows",status,()=>{offset=Math.Max(0,offset-100);Show();return Task.CompletedTask;}),
            Form.Action("Next imported source rows",status,()=>{offset=Math.Min(((rows.Count-1)/100)*100,offset+100);Show();return Task.CompletedTask;}),status,table);
        if(statement.EvidenceId.Length>0)panel.Add(Form.Text("Statement evidence: "+statement.EvidenceId));
        Show();return panel;
    }
}
