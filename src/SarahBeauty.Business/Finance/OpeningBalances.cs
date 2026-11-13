using SarahBeauty.Business.Security;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record OpeningBalance(string Id,string AccountId,string AccountName,string DimensionKind,string DimensionId,DateOnly Date,
    long Cents,bool Credit,string? MovementId,string Reason,string? RevokedAt)
{
    public override string ToString()=>$"{AccountName} • {Date:yyyy-MM-dd} • {Rules.Money.Display(Cents)} • {(RevokedAt is null?"Confirmed":"Revoked")}";
}

public sealed class OpeningBalances(BusinessStore store)
{
    public List<OpeningBalance> List(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return List(db,owner);});

    public void ConfirmZero(OwnerSession owner,string accountId,DateOnly date,string reason,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Record the evidence or reason for confirmed zero.");
        store.Write(owner,"Confirm zero cash opening",accountId,db=>
        {
            var account=ChartOfAccounts.List(db,owner).SingleOrDefault(a=>a.Id==accountId && a.Active && a.CashKind!="None")
                ??throw new ArgumentException("Choose an actual active cash, bank, or processor account.");
            PostingValidation.CheckClosedPeriod(db,owner,date);
            ReconciliationLocks.RequireWritable(db,owner,accountId,date);
            OpeningRules.ValidateTarget(db,owner,accountId,"Account","",date);
            Insert(db,owner,accountId,"Account","",date,0,false,null,reason.Trim(),"");
            return true;
        });
    }

    public void RevokeZero(OwnerSession owner,string id,string reason,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Record why this zero opening is being revoked.");
        store.Write(owner,"Revoke zero cash opening",id,db=>
        {
            var row=List(db,owner).SingleOrDefault(r=>r.Id==id && r.MovementId is null && r.RevokedAt is null)
                ??throw new ArgumentException("Choose a current zero opening. Reverse a posted opening through Money.");
            PostingValidation.CheckClosedPeriod(db,owner,row.Date);
            ReconciliationLocks.RequireWritable(db,owner,row.AccountId,row.Date);
            Revoke(db,owner,row.Id,row.AccountId,row.DimensionKind,reason.Trim(),null);
            return true;
        });
    }

    internal static void Insert(DbWork db,OwnerSession owner,string account,string kind,string dimension,DateOnly date,long cents,
        bool credit,string? movement,string reason,string evidence)
    {
        db.Execute("""
            INSERT INTO OpeningBalances (Id,BusinessId,AccountId,DimensionKind,DimensionId,Date,Cents,Credit,MovementId,Reason,Evidence,OwnerId,AtUtc)
            VALUES ($id,$business,$account,$kind,$dimension,$date,$cents,$credit,$movement,$reason,$evidence,$owner,$at);
            """,("$id",Guid.NewGuid().ToString()),("$business",owner.BusinessId),("$account",account),("$kind",kind),("$dimension",dimension),
            ("$date",date.ToStorageDate()),("$cents",cents),("$credit",credit?1:0),("$movement",movement),
            ("$reason",reason),("$evidence",evidence),("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        if(kind=="Account")db.Execute("UPDATE Accounts SET OpeningConfirmed=1 WHERE Id=$id;",("$id",account));
    }

    internal static void Revoke(DbWork db,OwnerSession owner,string id,string account,string kind,string reason,string? reversal)
    {
        db.Execute("UPDATE OpeningBalances SET RevokedAt=$at,RevokedBy=$owner,RevocationReason=$reason,ReversalMovementId=$reversal WHERE Id=$id;",
            ("$at",DateTimeOffset.UtcNow.ToString("O")),("$owner",owner.OwnerId),("$reason",reason),("$reversal",reversal),("$id",id));
        if(kind=="Account")db.Execute("UPDATE Accounts SET OpeningConfirmed=0 WHERE Id=$id;",("$id",account));
    }

    private static List<OpeningBalance> List(DbWork db,OwnerSession owner)=>db.Read("""
        SELECT O.Id,O.AccountId,A.Name,O.DimensionKind,O.DimensionId,O.Date,O.Cents,O.Credit,O.MovementId,O.Reason,O.RevokedAt
        FROM OpeningBalances O JOIN Accounts A ON A.Id=O.AccountId WHERE O.BusinessId=$business ORDER BY O.Date,O.AtUtc;
        """,r=>new OpeningBalance(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),StoredDates.Parse(r.GetString(5)),r.GetInt64(6),
            r.GetInt32(7)==1,r.IsDBNull(8)?null:r.GetString(8),r.GetString(9),r.IsDBNull(10)?null:r.GetString(10)),("$business",owner.BusinessId));
}
