using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class DocumentDecisionView
{
    private UIElement? _evidenceUi;
    private void Show(DocumentVersion version)
    {
        _selected=version;_retryDraft=null;_request="";_detail.Children.Clear();_editor.Children.Clear();_history.Children.Clear();
        _detail.Add(Form.Title(version.ToString()),Form.Text("Version "+version.Id+"\nIssued snapshot SHA-256 "+version.Hash),QuoteView.Snapshot(version.Snapshot),PdfArtifactView.Build(store,owner,version.Id));
        foreach(var value in _decisions.Where(d=>d.VersionId==version.Id))
        {
            _history.Add(Form.Title("Retained document decision"),Form.Text($"{value.Action} • {Date(value.DecisionDate)} • {value.PriorStatus} → {value.ResultStatus}\nRecorded by owner {value.OwnerId} at {value.RecordedAtUtc}\nPrivate reason: {value.Reason}\nDecision {value.Id} • Request {value.RequestId}"));
            if(value.Evidence is{} file)_history.Add(FinancialEvidenceView.Build(store,owner,file));
        }
        if(version.Status=="Issued")Editor(version);else _editor.Add(Form.Text("This version has no available terminal decision. Review its retained history or the booking's cancellation and adjustment workflows."));
        _baseline=Fields();
    }
    private void Editor(DocumentVersion version)
    {
        _date.Text=Date(DateOnly.FromDateTime(DateTime.Today));_reason.Text="";
        string[] actions=version.Snapshot.Kind==DocumentKind.Invoice?["Void unused invoice"]:["Decline quote","Withdraw quote","Void quote"];
        _action.ItemsSource=actions;_action.SelectedItem=actions[0];
        _evidence.Update(_files.Select(f=>(f.Version.Id,$"{f.Version.Name} • v{f.Version.Number} • {f.Version.OriginalFileName}")),"");
        _editor.Add(Form.Title("Prepare a document decision"),_action,_date,_reason,_evidenceUi??=_evidence.Build(),Form.Text("Optional evidence must be an exact saved receipt or evidence file. This private decision does not send a message, refund money, issue a credit note, or cancel a booking."),Form.Action("Review and record document decision",_status,()=>Run(Confirm)));
    }
    private DocumentDecisionDraft Read()
    {
        var version=_selected??throw new ArgumentException("Choose an issued document version.");
        if(!DateOnly.TryParseExact(_date.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new ArgumentException("Use a valid decision date in YYYY-MM-DD format.");
        var action=_action.SelectedItem?.ToString() switch{"Decline quote"=>DocumentDecisionAction.DeclineQuote,"Withdraw quote"=>DocumentDecisionAction.WithdrawQuote,"Void quote"=>DocumentDecisionAction.VoidQuote,"Void unused invoice"=>DocumentDecisionAction.VoidInvoice,_=>throw new ArgumentException("Choose a document decision.")};
        var evidence=_evidence.SelectedId.Length==0?null:ManagedEvidenceFile.From(_files.Single(f=>f.Version.Id==_evidence.SelectedId));
        return DocumentDecisionRules.Normalize(new(version.Id,version.Hash,version.Status,action,date,_reason.Text,evidence));
    }
    private static string Date(DateOnly value)=>value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
}
