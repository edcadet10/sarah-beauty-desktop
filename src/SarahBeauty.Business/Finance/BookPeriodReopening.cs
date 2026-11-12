using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BookPeriodReopening
{
    internal static BookPeriodReopen Reopen(DbWork db,OwnerSession owner,string id,string reason,string request,Action<string>? failure)
    {
        var prior=db.Read("""
            SELECT R.Id,R.PeriodId,R.Reason,R.OwnerId,R.AtUtc FROM BookPeriodReopens R JOIN BookPeriodCloses C ON C.Id=R.PeriodId
            WHERE C.BusinessId=$business AND R.RequestId=$request;
            """,r=>new BookPeriodReopen(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
        if(prior is not null)
        {
            if(prior.PeriodId!=id || prior.Reason!=reason)throw new ArgumentException("This reopen request already belongs to a different decision.");
            return prior;
        }
        var period=BookPeriodQueries.Open(db,owner,id);
        if(period.Reopened)throw new ArgumentException("This period is already reopened. Prepare a revised closing report.");
        if(BookPeriodRules.ClosedThrough(db,owner)!=period.Snapshot.Draft.Through)throw new ArgumentException("Reopen later closed book periods first.");
        var saved=new BookPeriodReopen(Guid.NewGuid().ToString(),id,reason,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        db.Execute("INSERT INTO BookPeriodReopens (Id,PeriodId,RequestId,Reason,OwnerId,AtUtc) VALUES ($id,$period,$request,$reason,$owner,$at);",
            ("$id",saved.Id),("$period",id),("$request",request),("$reason",reason),("$owner",owner.OwnerId),("$at",saved.AtUtc));
        failure?.Invoke("period-reopen");
        db.Execute("UPDATE BookSettings SET ClosedThrough=$date WHERE BusinessId=$business;",("$date",period.Snapshot.PreviouslyClosedThrough?.ToStorageDate()),("$business",owner.BusinessId));
        BusinessStore.Audit(db,owner,"Reopen book period",id);failure?.Invoke("audit");
        return saved;
    }
}
