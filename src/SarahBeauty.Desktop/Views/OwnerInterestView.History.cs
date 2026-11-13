using SarahBeauty.Business.Ownership;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class OwnerInterestView
{
    private void ShowHistory()
    {
        var superseded=_versions.Where(v=>v.SupersedesId is not null).Select(v=>v.SupersedesId).ToHashSet(StringComparer.Ordinal);
        _history.Content=PagedReport.Build("Ownership confirmation history",["Effective from","Revision / state","Confirmed shares","Reason","Recorded at UTC","Supporting file"],
            _versions.OrderByDescending(v=>v.Revision).Select(v=>new[]{Date(v.EffectiveFrom),v.Revision+" • "+(superseded.Contains(v.Id)?"Superseded; retained":"Current for this effective date"),
                Shares(v),v.Reason,v.RecordedAtUtc,EvidenceLabel(v)}));
    }
    private void ShowAtDate()
    {
        var date=Day(_reviewDate);var period=OwnerInterests.BuildPeriods(_versions).SingleOrDefault(p=>p.EffectiveFrom<=date && (p.EffectiveThrough is null || date<=p.EffectiveThrough));
        _review.Text=period is null?"Ownership at "+Date(date)+": Unknown / unconfirmed.":"Ownership at "+Date(date)+": "+Shares(period.Confirmation)+"\nEffective "+Date(period.EffectiveFrom)+" through "+(period.EffectiveThrough is DateOnly end?Date(end):"the next dated change")+". Based on saved revision "+period.Confirmation.Revision+".";
    }
    private static string Shares(OwnerInterestVersion value)=>string.Join(" • ",value.Shares.Select(s=>s.OwnerName+": "+Percent(s.Fraction)));
    private string EvidenceLabel(OwnerInterestVersion value)=>value.EvidenceVersionId is null?"None recorded":_files.FirstOrDefault(f=>f.Version.Id==value.EvidenceVersionId) is { } file?file.Version.Name+" • v"+file.Version.Number:"Retained supporting version";
}
