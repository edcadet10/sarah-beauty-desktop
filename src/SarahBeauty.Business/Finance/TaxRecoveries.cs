using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class TaxRecoveries
{
    internal static List<JournalLine> Lines(DbWork db,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        var source=CorrectionRules.Original(db,owner,draft.OriginalId);
        if(source.Kind!=FinancialKind.Refund || (source.TaxRefundAllocation?.RecoverableCents??0)==0 || draft.Date<source.Date
            || db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id;",("$id",draft.OriginalId)) is not null)
            throw new ArgumentException("Choose an unreversed refund with reviewed recoverable tax, and a date on or after it.");
        if(draft.TaxRecoveryGeneration!=ValueState.Generation(db,owner,"TaxRecovery",draft.OriginalId))
            throw new ArgumentException("The tax recovery changed. Save and review its remaining amount again.");
        if(string.IsNullOrWhiteSpace(draft.EvidenceId))throw new ArgumentException("Record evidence of the actual tax recovery, applied offset, or denied recovery.");
        if(draft.AmountCents>History(db,owner,draft.OriginalId).Where(h=>h.Date<=draft.Date).Sum(h=>h.Cents))
            throw new ArgumentException("The amount exceeds this refund's remaining recoverable tax.");
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        string debit=draft.TaxRecoveryDestination switch
        {
            TaxRecoveryDestination.Cash=>draft.CashAccountId,
            TaxRecoveryDestination.Offset=>Code("2000"),
            TaxRecoveryDestination.WriteOff=>Code("5050"),
            _=>throw new ArgumentException("Choose the actual recovery destination or write-off decision.")
        };
        return [new(debit,draft.AmountCents,0),new(Code("1250"),0,draft.AmountCents)];
    }

    internal static void AfterPosting(DbWork db,OwnerSession owner,FinancialDraft draft,string movement)
    {
        string? root=draft.Kind switch
        {
            FinancialKind.Refund=>movement,
            FinancialKind.TaxRecovery=>draft.OriginalId,
            FinancialKind.Reversal=>ReversalRoot(db,owner,draft.OriginalId),
            _=>null
        };
        if(root is null)return;
        long balance=0;
        foreach(var day in History(db,owner,root).GroupBy(h=>h.Date).OrderBy(g=>g.Key))
        {
            balance=checked(balance+day.Sum(h=>h.Cents));
            if(balance<0)throw new ArgumentException($"This refund's tax recovery would become negative on {day.Key:yyyy-MM-dd}.");
        }
        ValueState.Advance(db,owner,"TaxRecovery",root);
    }

    private static string? ReversalRoot(DbWork db,OwnerSession owner,string original)
    {
        var source=CorrectionRules.Original(db,owner,original);
        return source.Kind==FinancialKind.Refund?original:source.Kind==FinancialKind.TaxRecovery?source.OriginalId:null;
    }

    private static List<(DateOnly Date,long Cents)> History(DbWork db,OwnerSession owner,string root)=>db.Read("""
        SELECT M.Date,J.DebitCents-J.CreditCents FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id
        JOIN Accounts A ON A.Id=J.AccountId LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND A.Code='1250' AND
        ((M.Kind='Refund' AND M.Id=$root) OR (M.Kind='TaxRecovery' AND M.OriginalId=$root)
        OR (M.Kind='Reversal' AND ((O.Kind='Refund' AND O.Id=$root) OR (O.Kind='TaxRecovery' AND O.OriginalId=$root)))) ORDER BY M.Date;
        """,r=>(StoredDates.Parse(r.GetString(0)),r.GetInt64(1)),("$business",owner.BusinessId),("$root",root));
}
