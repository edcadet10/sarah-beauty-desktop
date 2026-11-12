using System.Text.Json;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class DebtSchedule
{
    internal static DebtScheduleSnapshot Read(DbWork db,BusinessStore store,OwnerSession owner,DateOnly from,DateOnly through)=>Read(db,owner.BusinessId,from,through);
    internal static DebtScheduleSnapshot Read(DbWork db,string businessId,DateOnly from,DateOnly through)
    {
        var loans=RecordStore<Loan>.ReadBusiness(db,businessId,"Loan");
        var entries=Entries(db,businessId,through);var groups=entries.ToLookup(e=>e.LoanId);
        var names=db.Read("SELECT Id,Name FROM Owners WHERE BusinessId=$business;",r=>(Id:r.GetString(0),Name:r.GetString(1)),("$business",businessId)).ToDictionary(o=>o.Id,o=>o.Name);
        return new(1,loans.Select(l=>Position(l,names.GetValueOrDefault(l.OwnerLenderId,""),groups[l.Id].ToList(),from)).ToList(),entries);
    }

    private static DebtSchedulePosition Position(Loan loan,string ownerName,List<DebtScheduleEntry> entries,DateOnly from)
    {
        var principal=entries.Where(e=>e.AccountCode=="2300").ToList();
        var period=entries.Where(e=>e.Date>=from).ToList();
        long Net(string kind)=>period.Where(e=>e.AccountCode=="2300" && e.EffectiveKind==kind).Sum(e=>e.CreditCents-e.DebitCents);
        long Expense(string code)=>period.Where(e=>e.AccountCode==code).Sum(e=>e.DebitCents-e.CreditCents);
        return new(Facts(loan,ownerName),principal.Count==0?null:principal.Where(e=>e.Date<from).Sum(e=>e.CreditCents-e.DebitCents),
            Net("Opening"),Net("LoanProceeds"),-Net("LoanPayment"),
            period.Where(e=>e.AccountCode=="2300" && e.EffectiveKind is not ("Opening" or "LoanProceeds" or "LoanPayment")).Sum(e=>e.CreditCents-e.DebitCents),
            Expense("5030"),Expense("5035"),principal.Count==0?null:principal.Sum(e=>e.CreditCents-e.DebitCents));
    }

    private static LoanRegisterFacts Facts(Loan loan,string ownerName)=>new(loan.Id,loan.Name,loan.Archived,loan.OpeningDate,loan.OpeningPrincipalCents,
        loan.DueDate,loan.Terms,loan.OwnerLenderId,ownerName,loan.EvidenceId,loan.Revision);

    private static List<DebtScheduleEntry> Entries(DbWork db,string businessId,DateOnly through)=>db.Read("""
        SELECT M.LoanId,M.Id,M.Date,M.Kind,CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END,M.Memo,
            A.Code,A.Name,J.DebitCents,J.CreditCents,json_extract(M.DataJson,'$.LoanLender')
        FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id JOIN Accounts A ON A.Id=J.AccountId
        LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND M.LoanId IS NOT NULL AND M.Date<=$through ORDER BY M.Date,M.rowid,J.rowid;
        """,r=>new DebtScheduleEntry(r.GetString(0),r.GetString(1),StoredDates.Parse(r.GetString(2)),r.GetString(3),r.GetString(4),r.GetString(5),
            r.GetString(6),r.GetString(7),r.GetInt64(8),r.GetInt64(9),r.IsDBNull(10)?null:JsonSerializer.Deserialize<LoanLenderSnapshot>(r.GetString(10))),
        ("$business",businessId),("$through",through.ToStorageDate()));
}
