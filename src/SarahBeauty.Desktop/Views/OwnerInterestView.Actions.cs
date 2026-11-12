using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Ownership;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class OwnerInterestView
{
    private void ResetDraft()
    {
        _draftRevision=_versions.LastOrDefault()?.Revision??0;_supersedes=null;_mode.Text="New effective-date confirmation";_effective.IsReadOnly=false;_effective.Text=Date(DateOnly.FromDateTime(DateTime.Today));
        _reason.Text="";_evidence.Update(_files.Select(f=>(f.Version.Id,f.Version.Name+" • v"+f.Version.Number)),"");_evidenceDetail.Content=null;_shares.Children.Clear();_inputs.Clear();
        foreach(var item in _owners){var field=Form.Input("Ownership percentage for "+item.Name);_inputs.Add(item.Id,field);_shares.Add(field);if(!item.LoginActive)_shares.Add(Form.Text(item.Name+" has a disabled login. Confirm their ownership independently."));}
        ShowRevision();
    }
    private void ShowRevision()=>_revision.Text=$"Saved ownership revision: {_versions.LastOrDefault()?.Revision??0}. Draft based on revision: {_draftRevision}.";
    private bool HasDraft()=>_reason.Text.Length>0 || _inputs.Values.Any(field=>field.Text.Length>0) || _evidence.SelectedId.Length>0 || _supersedes is not null || _effective.Text!=Date(DateOnly.FromDateTime(DateTime.Today));
    private async Task<bool> Ask(string title,string content,string accept)
    {
        if(!_loaded)return false;_dialog=new(){XamlRoot=_fields.XamlRoot,Title=title,Content=content,PrimaryButtonText=accept,CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        try{return await _dialog.ShowAsync()==ContentDialogResult.Primary;}finally{_dialog=null;}
    }
    private async Task StartNew(CancellationToken cancellation)
    {
        if(HasDraft()&&!await Ask("Discard the current ownership draft?","Saved confirmations remain in history.","Start a new draft"))return;
        cancellation.ThrowIfCancellationRequested();ResetDraft();_status.Text="New ownership draft started. Enter confirmed percentages.";
    }
    private async Task Reload(CancellationToken cancellation)
    {
        if(!await Ask("Reload ownership settings?","Unsaved ownership fields will be discarded. Saved confirmations remain in history.","Reload form"))return;
        await Load(cancellation,true);
    }
    private async Task LoadCorrection(CancellationToken cancellation)
    {
        var version=OwnerInterests.BuildPeriods(_versions).SingleOrDefault(p=>p.Confirmation.Id==_correction.SelectedId)?.Confirmation??throw new ArgumentException("Choose a current effective-date confirmation to correct.");
        if(HasDraft()&&!await Ask("Replace the current ownership draft?","Load the selected confirmation for a new correction. Saved versions remain in history.","Load correction"))return;
        cancellation.ThrowIfCancellationRequested();ResetDraft();_supersedes=version.Id;_effective.Text=Date(version.EffectiveFrom);_effective.IsReadOnly=true;_mode.Text="Correction of ownership revision "+version.Revision;
        foreach(var share in version.Shares)if(_inputs.TryGetValue(share.OwnerId,out var field))field.Text=(share.Fraction*100).ToString("0.####",CultureInfo.InvariantCulture);
        _evidence.Update(_files.Select(f=>(f.Version.Id,f.Version.Name+" • v"+f.Version.Number)),version.EvidenceVersionId??"");_status.Text="Review every percentage and enter a correction reason. The original confirmation will remain in history.";
    }
    private OwnerInterestDraft Draft()=>new(_draftRevision,Day(_effective),_inputs.Select(pair=>new OwnerInterestInput(pair.Key,Fraction(pair.Value))).ToArray(),_reason.Text,_supersedes,_evidence.SelectedId.Length==0?null:_evidence.SelectedId);
    private static decimal? Fraction(TextBox field)
    {
        if(string.IsNullOrWhiteSpace(field.Text))return null;
        if(!decimal.TryParse(field.Text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal percentage) || percentage<0 || percentage>100)throw new ArgumentException("Enter percentages from 0 to 100 using a decimal point.");
        return percentage/100;
    }
    private async Task Confirm(CancellationToken cancellation)
    {
        var draft=OwnerInterestRules.Normalize(Draft());string shares=string.Join("\n",draft.Shares.Select(s=>_owners.Single(o=>o.Id==s.OwnerId).Name+": "+Percent(s.Fraction!.Value)));
        if(!await Ask("Confirm these ownership percentages?",Date(draft.EffectiveFrom)+"\n"+shares+"\n\n"+draft.Reason+"\n\nThis records ownership settings only. Actual money and tax treatment remain separate.","Confirm percentages"))return;
        var saved=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new OwnerInterests(store).Confirm(owner,draft,token),token),cancellation);
        if(!_loaded)return;await Load(cancellation,true);if(_loaded)_status.Text="Ownership revision "+saved.Revision+" confirmed. Earlier versions remain in history.";
    }
    private async Task ShowEvidence(CancellationToken cancellation)
    {
        string id=_evidence.SelectedId;if(id.Length==0)throw new ArgumentException("Choose an ownership supporting file first.");
        var file=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new ManagedAttachments(store).Get(owner,id),token),cancellation);
        if(_loaded)_evidenceDetail.Content=EvidenceContentView.Build(store,owner,file.Version);
    }
}
