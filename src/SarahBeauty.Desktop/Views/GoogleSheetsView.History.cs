using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Google;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class GoogleSheetsView
{
    private void Refresh()
    {
        var saved=new GoogleSheetsExportProfiles(store).Current(owner);var health=new GoogleSheetsHealthService(store).Current(owner,saved?.Selection);
        _health.Text=$"Readable export: {health.Status} • Current watermark {health.CurrentWatermark}\nSaved-report coverage {health.ExportedWatermark?.ToString()??"None"} • Completed {health.CompletedAtUtc??"Never"}"+(health.FailureCode is null?"":" • "+health.FailureCode);
        var state=new GoogleSheetsQueue(store).Current(owner);_queue.Text=state is null?"Readable retry queue: Never observed.":$"Readable retry queue: {state.Status} • Observed {state.ObservedWatermark} • Exported {state.DeliveredWatermark?.ToString()??"None"}\nNext eligible attempt {state.NextAttemptAtUtc??"When eligible"} • Failures {state.FailureCount} • Code {state.FailureCode??"None"}";
        string selected=_history.SelectedId;_jobs=new GoogleSheetsDeliveries(store).History(owner);_history.Update(_jobs.Select(history=>(history.Job.Id,$"{history.Job.CreatedAtUtc} • Watermark {history.Job.SourceWatermark} • {history.Attempts.LastOrDefault()?.Status??"Pending"}")),selected);ShowRuntime();
    }
    private GoogleSheetsDeliveryHistory Selected()=>_jobs.SingleOrDefault(history=>history.Job.Id==_history.SelectedId)??throw new ArgumentException("Choose a saved readable export job.");
    private void Show()
    {
        var history=Selected();var job=history.Job;_detail.Text=$"Captured watermark {job.SourceWatermark} • Captured {job.CapturedAtUtc}\nFinancial reports {Date(job.Selection.FinancialFrom)} through {Date(job.Selection.FinancialThrough)} • Planning {Date(job.Selection.PlanningAsOf)}\nEvents {Date(job.Selection.EventFrom)} through {Date(job.Selection.EventThrough)}\nWorkbook creation {history.Creation?.State??"Not started"} • File {history.Creation?.FileId??"Unknown"}\n"+
            string.Join("\n",history.Attempts.Select(attempt=>$"{attempt.StartedAtUtc} • {attempt.Status} • Finished {attempt.FinishedAtUtc??"Unconfirmed"} • Code {attempt.FailureCode??"None"}"));
    }
    private async Task OpenVerified()
    {
        var saved=new GoogleSheetsExportProfiles(store).Current(owner);var health=new GoogleSheetsHealthService(store).Current(owner,saved?.Selection);string id=health.LastSuccess?.Attempt.FileId??throw new ArgumentException("No verified readable workbook is recorded for this destination.");
        if(!await Windows.System.Launcher.LaunchUriAsync(new Uri("https://docs.google.com/spreadsheets/d/"+Uri.EscapeDataString(id)+"/edit")))throw new IOException("The browser could not open the recorded workbook.");
        if(_loaded)_status.Text="Opened the last recorded verified workbook. Its current contents and sharing can change after delivery.";
    }
    private void ShowPreview(GoogleSheetsExportPreview preview)
    {
        var panel=Form.Panel();panel.Add(Form.Text($"Local preview • {preview.Mode} • Captured watermark {preview.SourceWatermark} • {preview.CapturedAtUtc}"),
            Form.Text($"Financial reports {Date(preview.Selection.FinancialFrom)} through {Date(preview.Selection.FinancialThrough)} • Planning {Date(preview.Selection.PlanningAsOf)}\nEvents {Date(preview.Selection.EventFrom)} through {Date(preview.Selection.EventThrough)}"),
            Form.Text($"Source tables {preview.Tables.Count:N0} plus one delivery-status tab • Data rows {preview.DataRows:N0}\nPlanned workbook cells {preview.PlannedWorkbookCells:N0} • Source text {preview.RawUtf8Bytes:N0} UTF-8 bytes\nJournal lines {preview.JournalLines:N0} • Debits {preview.JournalDebitCents} cents • Credits {preview.JournalCreditCents} cents (USD)"));
        foreach(string notice in preview.Notices)panel.Add(Form.Text(notice));
        panel.Add(Form.Text("This preview does not send data or freeze later edits. Export captures and verifies a new generation."),new Expander{Header="Tables in this local preview",Content=new ScrollViewer{MaxHeight=300,Content=Form.Text(string.Join("\n",preview.Tables.Select(table=>$"{table.Name} • {table.DataRows:N0} data rows • {table.Columns} columns")))}});_preview.Content=panel;
    }
}
