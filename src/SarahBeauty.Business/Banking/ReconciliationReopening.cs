using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class ReconciliationReopening
{
    internal static ReconciliationReopen Reopen(DbWork db,OwnerSession owner,string id,string reason,string request,Action<string>? failure)
    {
        var existing=db.Read("""
            SELECT R.Id,R.ReconciliationId,R.Reason,R.OwnerId,R.AtUtc FROM ReconciliationReopens R JOIN Reconciliations C ON C.Id=R.ReconciliationId
            WHERE C.BusinessId=$business AND R.RequestId=$request;
            """,r=>new ReconciliationReopen(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
        if(existing is not null)
        {
            if(existing.ReconciliationId!=id || existing.Reason!=reason)throw new ArgumentException("This reopen request already belongs to a different decision.");
            return existing;
        }
        var target=ReconciliationQueries.List(db,owner,id).SingleOrDefault()??throw new ArgumentException("Choose a completed reconciliation from this business.");
        if(target.Reopened)throw new ArgumentException("This reconciliation is already reopened. Retain its snapshot and prepare a revised completion.");
        PostingValidation.CheckClosedPeriod(db,owner,target.AsOfDate);
        if(db.Scalar("""
            SELECT C.Id FROM Reconciliations C WHERE C.BusinessId=$business AND C.AccountId=$account AND C.AsOfDate>$date
            AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id) LIMIT 1;
            """,("$business",owner.BusinessId),("$account",target.AccountId),("$date",target.AsOfDate.ToString("yyyy-MM-dd"))) is not null)
            throw new ArgumentException("Reopen this account's later reconciliations first so their dependencies remain explicit.");
        string reopen=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O");
        db.Execute("INSERT INTO ReconciliationReopens (Id,ReconciliationId,RequestId,Reason,OwnerId,AtUtc) VALUES ($id,$target,$request,$reason,$owner,$at);",
            ("$id",reopen),("$target",id),("$request",request),("$reason",reason),("$owner",owner.OwnerId),("$at",at));
        failure?.Invoke("reconciliation-reopen");
        StatementErrorHistory.Advance(db,owner,target.Snapshot.Draft);
        BusinessStore.Audit(db,owner,"Reopen reconciliation",id);failure?.Invoke("audit");
        return new(reopen,id,reason,owner.OwnerId,at);
    }
}
