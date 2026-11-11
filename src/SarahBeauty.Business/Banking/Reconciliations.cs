using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

public sealed class Reconciliations(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public List<ReconciliationSummary> History(OwnerSession owner)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return db.Read("""
            SELECT C.Id,C.AccountId,C.StatementId,C.AsOfDate,C.ApprovedBy,C.AtUtc,EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id)
            FROM Reconciliations C WHERE C.BusinessId=$business ORDER BY C.AsOfDate,C.AtUtc,C.Id;
            """,r=>new ReconciliationSummary(r.GetString(0),r.GetString(1),r.GetString(2),DateOnly.Parse(r.GetString(3)),r.GetString(4),r.GetString(5),r.GetInt32(6)==1),("$business",owner.BusinessId));
    });

    public CompletedReconciliation Open(OwnerSession owner,string id)=>store.ReadSnapshot(db=>
    {store.RequireSession(db,owner);return ReconciliationQueries.List(db,owner,id).SingleOrDefault()??throw new ArgumentException("Choose a saved reconciliation from this business.");});

    public List<ReconciliationReopen> ReopenHistory(OwnerSession owner,string id)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return db.Read("""
            SELECT R.Id,R.ReconciliationId,R.Reason,R.OwnerId,R.AtUtc FROM ReconciliationReopens R JOIN Reconciliations C ON C.Id=R.ReconciliationId
            WHERE C.BusinessId=$business AND R.ReconciliationId=$id ORDER BY R.AtUtc;
            """,r=>new ReconciliationReopen(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)),("$business",owner.BusinessId),("$id",id));
    });

    public ReconciliationSources Sources(OwnerSession owner,string statement)=>store.ReadSnapshot(db=>
    {store.RequireSession(db,owner);return ReconciliationSourcesReader.Read(db,owner,statement);});

    public ReconciliationPreview Preview(OwnerSession owner,ReconciliationDraft draft)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        draft=Normalize(draft);
        var preview=ReconciliationCalculator.Calculate(ReconciliationSourcesReader.Read(db,owner,draft.StatementId),draft);
        StatementErrorHistory.CheckProposed(db,owner,preview);
        return preview;
    });

    public CompletedReconciliation Complete(OwnerSession owner,ReconciliationDraft draft,long reviewedWatermark,string requestId,string phrase)
    {
        requestId=ConfirmRequest(owner,requestId,phrase);
        draft=Normalize(draft);
        return store.Transaction(db=>{store.RequireSession(db,owner);return ReconciliationCompleter.Complete(db,owner,draft,reviewedWatermark,requestId,FailurePoint);});
    }

    public ReconciliationReopen Reopen(OwnerSession owner,string id,string reason,string requestId,string phrase)
    {
        requestId=ConfirmRequest(owner,requestId,phrase);
        reason=reason.Trim();
        if(reason.Length==0)throw new ArgumentException("Record the reason for reopening this reconciliation.");
        return store.Transaction(db=>{store.RequireSession(db,owner);return ReconciliationReopening.Reopen(db,owner,id,reason,requestId,FailurePoint);});
    }

    private string ConfirmRequest(OwnerSession owner,string request,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(!Guid.TryParse(request,out var id))throw new ArgumentException("Use a permanent request ID for this reviewed action.");
        return id.ToString();
    }

    internal static ReconciliationDraft Normalize(ReconciliationDraft draft)=>draft with{StatementId=draft.StatementId.Trim(),Reason=draft.Reason.Trim(),
        Outstanding=draft.Outstanding.Select(o=>o with{JournalLineId=o.JournalLineId.Trim(),Explanation=o.Explanation.Trim(),EvidenceId=o.EvidenceId.Trim()}).OrderBy(o=>o.JournalLineId,StringComparer.Ordinal).ToArray(),
        PostedCorrectionIds=draft.PostedCorrectionIds.Select(id=>id.Trim()).OrderBy(id=>id,StringComparer.Ordinal).ToArray(),
        StatementErrors=draft.StatementErrors.Select(e=>e with{Id=ErrorId(e.Id),BankLineId=e.BankLineId.Trim(),Explanation=e.Explanation.Trim(),EvidenceId=e.EvidenceId.Trim()}).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray(),
        BankCorrections=draft.BankCorrections.Select(r=>r with{ErrorId=ErrorId(r.ErrorId),BankLineId=r.BankLineId.Trim(),Explanation=r.Explanation.Trim(),EvidenceId=r.EvidenceId.Trim()})
            .OrderBy(r=>r.ErrorId,StringComparer.Ordinal).ThenBy(r=>r.BankLineId,StringComparer.Ordinal).ToArray()};

    private static string ErrorId(string id)=>Guid.TryParse(id,out var value)?value.ToString():id.Trim();
}
