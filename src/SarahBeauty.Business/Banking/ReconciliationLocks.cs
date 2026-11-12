using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class ReconciliationLocks
{
    internal static void RequireWritable(DbWork db,OwnerSession owner,string account,DateOnly date)
    {
        if(db.Scalar("""
            SELECT C.Id FROM Reconciliations C WHERE C.BusinessId=$business AND C.AccountId=$account AND C.AsOfDate>=$date
            AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id) LIMIT 1;
            """,("$business",owner.BusinessId),("$account",account),("$date",date.ToStorageDate())) is not null)
            throw new ArgumentException("Reopen this account's completed reconciliation before changing a date it covers.");
    }

    internal static void Posting(DbWork db,OwnerSession owner,DateOnly date,List<JournalLine> lines,List<Account> accounts)
    {
        var cash=accounts.Where(a=>a.CashKind!="None").Select(a=>a.Id).ToHashSet();
        foreach(string id in lines.Where(l=>cash.Contains(l.AccountId)).Select(l=>l.AccountId).Distinct())RequireWritable(db,owner,id,date);
    }

    internal static void Import(DbWork db,OwnerSession owner,StatementImportPreview preview,StatementRowChoice[] choices)
    {
        PostingValidation.CheckClosedPeriod(db,owner,preview.Draft.Through);
        RequireWritable(db,owner,preview.Draft.AccountId,preview.Draft.Through);
        var existing=choices.Where(c=>c.ExistingLineId.Length>0).Select(c=>c.SourceRow).ToHashSet();
        foreach(var row in preview.Rows.Where(r=>!existing.Contains(r.Row.SourceRow)))
        {
            PostingValidation.CheckClosedPeriod(db,owner,row.Row.Date);
            RequireWritable(db,owner,preview.Draft.AccountId,row.Row.Date);
        }
    }
}
