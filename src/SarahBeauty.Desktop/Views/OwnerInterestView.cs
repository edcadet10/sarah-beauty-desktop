using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Ownership;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class OwnerInterestView(BusinessStore store,OwnerSession owner)
{
    private readonly TextBox _effective=Form.Input("Ownership effective date (YYYY-MM-DD)"),_reason=Form.Input("Ownership confirmation or correction reason"),_reviewDate=Form.Input("Review ownership at date (YYYY-MM-DD)");
    private readonly TextBlock _status=Form.Text(),_mode=Form.Text(),_revision=Form.Text(),_review=Form.Text();
    private readonly StackPanel _shares=Form.Panel(),_fields=Form.Panel();
    private readonly ContentControl _history=new(),_evidenceDetail=new(),_fieldControl=new();
    private readonly SearchableRecords _correction=new("Ownership confirmation to correct"),_evidence=new("Ownership supporting file", "No supporting file selected");
    private readonly Dictionary<string,TextBox> _inputs=[];
    private IReadOnlyList<OwnerInterestVersion> _versions=[];
    private IReadOnlyList<OwnerInterestOwner> _owners=[];
    private IReadOnlyList<StoredAttachment> _files=[];
    private long _draftRevision;
    private string? _supersedes;
    private bool _loaded,_initialized;
    private CancellationTokenSource? _cancellation;
    private ContentDialog? _dialog;

    internal UIElement Build()
    {
        _reviewDate.Text=Date(DateOnly.FromDateTime(DateTime.Today));_fieldControl.Content=_fields;_fieldControl.IsEnabled=false;
        _fields.Add(_mode,_revision,_effective,Form.Text("Enter every owner's percentage from 0 to 100, using a decimal point and at most four decimal places. Blank means unknown; enter 0 only when confirmed. The total must be exactly 100%."),
            _shares,_reason,_evidence.Build(),Form.Action("Review ownership supporting file",_status,()=>Run(ShowEvidence)),_evidenceDetail,
            Form.Action("Confirm dated ownership",_status,()=>Run(Confirm)),
            _correction.Build(),Form.Action("Load selected ownership correction",_status,()=>Run(LoadCorrection)),
            Form.Action("Start new dated ownership confirmation",_status,()=>Run(StartNew)),
            Form.Action("Refresh saved ownership history",_status,()=>Run(c=>Load(c,false))),
            Form.Action("Reload ownership form and discard draft",_status,()=>Run(Reload)),
            _reviewDate,Form.Action("Review ownership at date",_status,()=>{ShowAtDate();return Task.CompletedTask;}),_review,_history);
        var panel=Form.Panel();panel.Add(Form.Title("Ownership settings"),Form.Text("Private, dated ownership confirmations. Unknown percentages stay unconfirmed. These settings do not distribute cash, allocate taxable profit, or establish tax basis. Actual contributions, drawings, loans, and reimbursements remain separate."),
            _fieldControl,Form.Action("Cancel ownership operation",_status,()=>{_dialog?.Hide();_cancellation?.Cancel();return Task.CompletedTask;}),_status);
        panel.Loaded+=async(_,_)=>{_loaded=true;if(_initialized)return;try{await Run(c=>Load(c,true));}catch(Exception error){if(_loaded)_status.Text=error.Message;}};
        panel.Unloaded+=(_,_)=>{_loaded=false;_dialog?.Hide();_cancellation?.Cancel();};return panel;
    }
    private async Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current ownership operation.");_cancellation=new();_fieldControl.IsEnabled=false;
        try{await action(_cancellation.Token);}
        catch(OperationCanceledException){if(_loaded)_status.Text="Ownership operation cancelled. Refresh saved history before retrying a confirmation.";}
        finally{_fieldControl.IsEnabled=_initialized;_cancellation.Dispose();_cancellation=null;}
    }
    private async Task Load(CancellationToken cancellation,bool reset)
    {
        var data=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();var book=new OwnerInterests(store);var owners=book.Owners(owner);var versions=book.History(owner);var files=new ManagedAttachments(store).History(owner).Where(f=>f.Version.Category!="Customer photo").ToArray();
            token.ThrowIfCancellationRequested();return(owners,versions,files);
        },token),cancellation);
        if(!_loaded)return;_owners=data.owners;_versions=data.versions;_files=data.files;
        _evidence.Update(_files.Select(f=>(f.Version.Id,f.Version.Name+" • v"+f.Version.Number)));
        _correction.Update(OwnerInterests.BuildPeriods(_versions).Select(p=>(p.Confirmation.Id,Date(p.EffectiveFrom)+" • revision "+p.Confirmation.Revision)));
        if(reset || !_initialized)ResetDraft();_initialized=true;ShowHistory();ShowAtDate();ShowRevision();
        _status.Text=reset?"Saved ownership loaded. Percentages remain blank until you enter a confirmation.":"Saved history refreshed. Your draft and its original revision are preserved.";
    }
    private static string Date(DateOnly value)=>value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    private static DateOnly Day(TextBox field)=>DateOnly.TryParseExact(field.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var value)?value:throw new ArgumentException("Enter ownership dates as YYYY-MM-DD.");
    private static string Percent(decimal fraction)=>(fraction*100).ToString("0.####",CultureInfo.InvariantCulture)+"%";
}
