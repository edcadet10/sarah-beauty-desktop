using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class OwnerSchedule
{
    internal const string OwnerLedger="Actual owner transaction";
    internal const string LoanLender="Captured loan lender";
    internal const string ConfirmedLoan="Captured owner lender confirmed";

    internal static OwnerScheduleSnapshot Read(DbWork db,OwnerSession owner,DateOnly from,DateOnly through,DebtScheduleSnapshot debts)=>Read(db,owner.BusinessId,from,through,debts);
    internal static OwnerScheduleSnapshot Read(DbWork db,string businessId,DateOnly from,DateOnly through,DebtScheduleSnapshot debts)
    {
        var owners=db.Read("SELECT Id,Name,Active FROM Owners WHERE BusinessId=$business ORDER BY Name,Id;",
            r=>(Id:r.GetString(0),Name:r.GetString(1),Active:r.GetInt32(2)==1),("$business",businessId));
        var entries=Entries(db,businessId,through);
        entries.AddRange(debts.Entries.Where(e=>e.CapturedLender?.OwnerLenderId.Length>0).Select(e=>new OwnerScheduleEntry(
            e.CapturedLender!.OwnerLenderId,e.MovementId,e.Date,e.Kind,e.EffectiveKind,e.Memo,e.AccountCode,e.AccountName,e.DebitCents,e.CreditCents,LoanLender,e.LoanId)));
        var loans=debts.Positions.Select(p=>LoanBalance(p,debts.Entries.Where(e=>e.LoanId==p.CurrentFacts.LoanId && e.AccountCode=="2300").ToList())).ToList();
        var positions=owners.Select(o=>Position(o.Id,o.Name,o.Active,entries.Where(e=>e.OwnerId==o.Id && e.Date>=from).ToList(),loans)).ToList();
        var accounts=owners.SelectMany(o=>new[]{"3000","3100","2100"}.Select(code=>Account(o.Id,code,entries,from))).ToList();
        return new(1,positions,accounts,entries,loans);
    }

    private static OwnerSchedulePosition Position(string id,string name,bool active,List<OwnerScheduleEntry> period,List<OwnerLoanBalance> loans)
    {
        var actual=period.Where(e=>e.Attribution==OwnerLedger).ToList();
        long Net(string code)=>actual.Where(e=>e.AccountCode==code && e.EffectiveKind!="Opening").Sum(e=>e.CreditCents-e.DebitCents);
        long LoanNet(string kind)=>period.Where(e=>e.Attribution==LoanLender && e.AccountCode=="2300" && e.EffectiveKind==kind).Sum(e=>e.CreditCents-e.DebitCents);
        var payable=actual.Where(e=>e.AccountCode=="2100").ToList();
        return new(id,name,active,Net("3000"),-Net("3100"),payable.Sum(e=>e.CreditCents),payable.Sum(e=>e.DebitCents),
            payable.Where(e=>e.EffectiveKind=="OwnerReimbursement").Sum(e=>e.DebitCents-e.CreditCents),LoanNet("LoanProceeds"),-LoanNet("LoanPayment"),
            loans.Where(l=>l.OwnerId==id && l.Attribution==ConfirmedLoan).Sum(l=>l.PrincipalCents!.Value));
    }

    private static OwnerAccountPosition Account(string owner,string code,List<OwnerScheduleEntry> entries,DateOnly from)
    {
        var rows=entries.Where(e=>e.OwnerId==owner && e.Attribution==OwnerLedger && e.AccountCode==code).ToList();
        return new(owner,code,rows.Where(e=>e.Date<from).Sum(e=>e.DebitCents-e.CreditCents),rows.Where(e=>e.Date>=from).Sum(e=>e.DebitCents),
            rows.Where(e=>e.Date>=from).Sum(e=>e.CreditCents),rows.Sum(e=>e.DebitCents-e.CreditCents));
    }

    private static OwnerLoanBalance LoanBalance(DebtSchedulePosition loan,List<DebtScheduleEntry> entries)
    {
        OwnerLoanBalance Result(string owner,string status)=>new(loan.CurrentFacts.LoanId,loan.CurrentFacts.Name,owner,status,loan.ClosingPrincipalCents);
        if(loan.ClosingPrincipalCents is null)return Result("","No approved principal history");
        if(entries.Any(e=>e.CapturedLender is null))return Result("","Historical lender attribution unconfirmed");
        var ids=entries.Select(e=>e.CapturedLender!.OwnerLenderId).Distinct().ToArray();
        if(ids.Length!=1)return Result("","Mixed captured lender identities require review");
        return ids[0].Length==0?Result("","No owner-lender link captured"):Result(ids[0],ConfirmedLoan);
    }

    private static List<OwnerScheduleEntry> Entries(DbWork db,string businessId,DateOnly through)=>db.Read("""
        SELECT COALESCE(M.OwnerId,''),M.Id,M.Date,M.Kind,CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END,M.Memo,
            A.Code,A.Name,J.DebitCents,J.CreditCents FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id
        JOIN Accounts A ON A.Id=J.AccountId LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND M.Date<=$through AND (A.Code IN('3000','3100','2100') OR
            (M.OwnerId IS NOT NULL AND EXISTS(SELECT 1 FROM JournalLines K JOIN Accounts B ON B.Id=K.AccountId
                WHERE K.MovementId=M.Id AND B.Code IN('3000','3100','2100')))) ORDER BY M.Date,M.rowid,J.rowid;
        """,r=>new OwnerScheduleEntry(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),r.GetString(3),r.GetString(4),r.GetString(5),
            r.GetString(6),r.GetString(7),r.GetInt64(8),r.GetInt64(9),OwnerLedger),
        ("$business",businessId),("$through",through.ToString("yyyy-MM-dd")));
}
