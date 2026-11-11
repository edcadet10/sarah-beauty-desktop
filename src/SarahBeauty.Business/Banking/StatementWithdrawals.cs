using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

public sealed record StatementWithdrawal(string Id,string StatementId,string Reason,string EvidenceId,string OwnerId,string AtUtc);
public sealed record StatementWithdrawalReview(StatementImport Statement,long Generation,int UnsharedLineCount,int MatchedLineCount);

public sealed class StatementWithdrawals(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public List<StatementWithdrawal> History(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return Read(db,owner);});

    public StatementWithdrawalReview Review(OwnerSession owner,string statementId)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        var statement=StatementQueries.Imports(db,owner,statementId).SingleOrDefault()??throw new ArgumentException("Choose an imported statement from this business.");
        var orphaned=OrphanedLines(db,statementId);
        var history=BankMatchQueries.History(db,owner).ToLookup(m=>m.BankLineId);
        return new StatementWithdrawalReview(statement,ValueState.Generation(db,owner,"StatementImport",statement.AccountId),orphaned.Length,
            orphaned.Count(id=>BankMatchQueries.Matched(history[id],DateOnly.MaxValue)!=0));
    });

    public StatementWithdrawal Approve(OwnerSession owner,string statementId,long reviewedGeneration,string reason,string evidenceId,string requestId,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(!Guid.TryParse(requestId,out var request))throw new ArgumentException("Use a permanent request ID for this statement withdrawal.");
        reason=reason.Trim();evidenceId=evidenceId.Trim();
        if(reason.Length==0 || evidenceId.Length==0)throw new ArgumentException("Record the reason and evidence for withdrawing an incorrect import.");
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var existing=Read(db,owner,request.ToString()).SingleOrDefault();
            if(existing is not null)
            {
                if(existing.StatementId!=statementId || existing.Reason!=reason || existing.EvidenceId!=evidenceId)throw new ArgumentException("This withdrawal request already belongs to a different decision.");
                return existing;
            }
            var statement=StatementQueries.Imports(db,owner,statementId).SingleOrDefault()??throw new ArgumentException("Choose an imported statement from this business.");
            if(statement.Withdrawn)throw new ArgumentException("This import is already withdrawn. Its original source and withdrawal decision remain available.");
            if(ValueState.Generation(db,owner,"StatementImport",statement.AccountId)!=reviewedGeneration)throw new ArgumentException("The account's imported sources changed. Refresh and review the withdrawal again.");
            PostingValidation.CheckClosedPeriod(db,owner,statement.From);
            ReconciliationLocks.RequireWritable(db,owner,statement.AccountId,statement.From);
            string[] orphaned=OrphanedLines(db,statementId);
            var history=BankMatchQueries.History(db,owner).ToLookup(m=>m.BankLineId);
            if(orphaned.Any(id=>BankMatchQueries.Matched(history[id],DateOnly.MaxValue)!=0))throw new ArgumentException("Unmatch every active portion of this import's otherwise unreferenced bank lines before withdrawing it, including future matching.");
            return Save(db,owner,statement,orphaned,reason,evidenceId,request.ToString());
        });
    }

    private StatementWithdrawal Save(DbWork db,OwnerSession owner,StatementImport statement,string[] orphaned,string reason,string evidence,string request)
    {
        var saved=new StatementWithdrawal(Guid.NewGuid().ToString(),statement.Id,reason,evidence,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        db.Execute("INSERT INTO StatementWithdrawals (Id,StatementId,RequestId,Reason,EvidenceId,OwnerId,AtUtc) VALUES ($id,$statement,$request,$reason,$evidence,$owner,$at);",
            ("$id",saved.Id),("$statement",statement.Id),("$request",request),("$reason",reason),("$evidence",evidence),("$owner",owner.OwnerId),("$at",saved.AtUtc));
        FailurePoint?.Invoke("statement-withdrawal");
        ValueState.Advance(db,owner,"StatementImport",statement.AccountId);
        foreach(string id in orphaned)ValueState.Advance(db,owner,"BankMatchBank",id);
        BusinessStore.Audit(db,owner,"Withdraw incorrect statement import",statement.Id);FailurePoint?.Invoke("audit");
        return saved;
    }

    private static string[] OrphanedLines(DbWork db,string statement)=>db.Read("""
        SELECT R.BankLineId FROM StatementRows R WHERE R.StatementId=$statement AND NOT EXISTS(
            SELECT 1 FROM StatementRows S WHERE S.BankLineId=R.BankLineId AND S.StatementId<>$statement
            AND NOT EXISTS(SELECT 1 FROM StatementWithdrawals W WHERE W.StatementId=S.StatementId));
        """,r=>r.GetString(0),("$statement",statement)).ToArray();

    private static List<StatementWithdrawal> Read(DbWork db,OwnerSession owner,string? request=null)=>db.Read("""
        SELECT W.Id,W.StatementId,W.Reason,W.EvidenceId,W.OwnerId,W.AtUtc FROM StatementWithdrawals W JOIN StatementImports S ON S.Id=W.StatementId
        WHERE S.BusinessId=$business AND ($request IS NULL OR W.RequestId=$request) ORDER BY W.AtUtc,W.Id;
        """,r=>new StatementWithdrawal(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5)),("$business",owner.BusinessId),("$request",request));
}
