using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

public sealed class Reconciliations(BusinessStore store)
{
    public ReconciliationSources Sources(OwnerSession owner,string statement)=>store.ReadSnapshot(db=>
    {store.RequireSession(db,owner);return ReconciliationSourcesReader.Read(db,owner,statement);});

    public ReconciliationPreview Preview(OwnerSession owner,ReconciliationDraft draft)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        draft=Normalize(draft);
        return ReconciliationCalculator.Calculate(ReconciliationSourcesReader.Read(db,owner,draft.StatementId),draft);
    });

    internal static ReconciliationDraft Normalize(ReconciliationDraft draft)=>draft with{StatementId=draft.StatementId.Trim(),Reason=draft.Reason.Trim(),
        Outstanding=draft.Outstanding.Select(o=>o with{JournalLineId=o.JournalLineId.Trim(),Explanation=o.Explanation.Trim(),EvidenceId=o.EvidenceId.Trim()}).OrderBy(o=>o.JournalLineId,StringComparer.Ordinal).ToArray(),
        PostedCorrectionIds=draft.PostedCorrectionIds.Select(id=>id.Trim()).OrderBy(id=>id,StringComparer.Ordinal).ToArray()};
}
