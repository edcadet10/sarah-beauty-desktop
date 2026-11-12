using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class TaxReportKernel
{
    internal static TaxPosition Read(DbWork db,string businessId,DateOnly from,DateOnly through,List<AccountTotal> trial,List<DocumentVersion> versions)
    {
        var documents=versions.Where(v=>v.Snapshot.IssueDate>=from && v.Snapshot.IssueDate<=through).ToList();
        var tax=db.Read("""
            SELECT CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END,SUM(J.CreditCents-J.DebitCents)
            FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id JOIN Accounts A ON A.Id=J.AccountId LEFT JOIN Movements O ON O.Id=M.ReversesId
            WHERE M.BusinessId=$business AND A.Code='2000' AND M.Date BETWEEN $from AND $through GROUP BY CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END;
            """,r=>(Kind:r.GetString(0),Cents:r.GetInt64(1)),("$business",businessId),("$from",from.ToStorageDate()),("$through",through.ToStorageDate()));
        var refunds=db.Read("""
            SELECT CASE WHEN M.Kind='Reversal' THEN -O.TaxCents ELSE M.TaxCents END FROM Movements M
            LEFT JOIN Movements O ON O.Id=M.ReversesId WHERE M.BusinessId=$business AND M.Date BETWEEN $from AND $through
            AND (M.Kind='Refund' OR (M.Kind='Reversal' AND O.Kind='Refund'));
            """,r=>r.GetInt64(0),("$business",businessId),("$from",from.ToStorageDate()),("$through",through.ToStorageDate()));
        var recovery=TaxRecoveryActivity(db,businessId,from,through);
        return new(documents.Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void").Sum(v=>v.Snapshot.TaxCents),
            documents.Where(v=>v.Snapshot.Kind==DocumentKind.CreditNote).Sum(v=>v.Snapshot.TaxCents),tax.Where(t=>t.Kind is "Receipt" or "AssetDisposal" or "ReceiptClassification").Sum(t=>t.Cents),
            refunds.Sum(),-tax.Where(t=>t.Kind=="TaxRemittance").Sum(t=>t.Cents),
            -trial.Where(r=>r.Account.Code=="2000").Sum(r=>r.Ending))
        {
            RecoverableCents=trial.Where(r=>r.Account.Code=="1250").Sum(r=>r.Ending),
            RecoveredCashCents=recovery.Where(r=>r.Destination==TaxRecoveryDestination.Cash).Sum(r=>r.Cents),
            OffsetCents=recovery.Where(r=>r.Destination==TaxRecoveryDestination.Offset).Sum(r=>r.Cents),
            NonrecoverableCents=trial.Where(r=>r.Account.Code=="5050").Sum(r=>r.Debits-r.Credits)
        };
    }

    private static List<(string Destination,long Cents)> TaxRecoveryActivity(DbWork db,string businessId,DateOnly from,DateOnly through)=>db.Read("""
        SELECT json_extract(CASE WHEN M.Kind='Reversal' THEN O.DataJson ELSE M.DataJson END,'$.TaxRecoveryDestination'),
            CASE WHEN M.Kind='Reversal' THEN -O.GrossCents ELSE M.GrossCents END FROM Movements M LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND M.Date BETWEEN $from AND $through AND (M.Kind='TaxRecovery' OR (M.Kind='Reversal' AND O.Kind='TaxRecovery'));
        """,r=>(r.GetString(0),r.GetInt64(1)),("$business",businessId),("$from",from.ToStorageDate()),("$through",through.ToStorageDate()));

}
