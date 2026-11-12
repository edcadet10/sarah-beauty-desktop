using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class ReconciliationContinuity
{
    internal static string[] Check(DbWork db,OwnerSession owner,StatementImport statement)
    {
        var latest=db.Read("""
            SELECT C.Id,C.AsOfDate FROM Reconciliations C WHERE C.BusinessId=$business AND C.AccountId=$account
            AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id) ORDER BY C.AsOfDate DESC LIMIT 1;
            """,r=>(Id:r.GetString(0),Date:StoredDates.Parse(r.GetString(1))),("$business",owner.BusinessId),("$account",statement.AccountId)).SingleOrDefault();
        if(latest.Id is not null)
        {
            if(latest.Date>=statement.Through)return ["A completed reconciliation already covers this date. Open its snapshot or reopen the affected reconciliations before revising."];
            if(statement.From>latest.Date.AddDays(1))return [$"The statement period leaves an unreconciled gap after {latest.Date:yyyy-MM-dd}."];
            var prior=ReconciliationQueries.List(db,owner,latest.Id).Single();
            long anchor=ReconciliationSourcesReader.Cents(StatementQueries.SourceRows(db,owner,statement.Id).Where(r=>r.Source.Date<=latest.Date)
                .Select(r=>r.Source.SignedCents).Prepend(statement.OpeningCents));
            return anchor==prior.Snapshot.Sources.Statement.ClosingCents?[]:["The statement balance at the prior reconciliation date disagrees with its saved closing balance."];
        }
        var opening=ReconciliationSourcesReader.OpeningDate(db,owner,statement.AccountId,statement.Through);
        return opening.HasValue && statement.From>opening.Value?[$"Begin with a statement period covering the account opening on {opening.Value:yyyy-MM-dd}, then reconcile in date order."]:[];
    }
}
