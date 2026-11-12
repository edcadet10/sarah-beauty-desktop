using System.Text.Json;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableReconciliationTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var balances=new ReadableTableBuilder(source,budget,"Reconciliation frozen balances","ReconciliationId","BusinessId","ObservedMode","ObservedWatermark","GeneratedAtUtc","OpeningConfirmed","StatementClosingCents","LedgerClosingCents","OutstandingDepositsCents","OutstandingPaymentsCents","PostedBookCorrectionsCents","LedgerBeforeCorrectionsCents","StatementErrorAdjustmentCents","AdjustedStatementCents","DifferenceCents","ReadyToComplete");
        var positions=new ReadableTableBuilder(source,budget,"Reconciliation frozen positions","ReconciliationId","BusinessId","Side","SourceId","Date","SignedCents","RemainingCents","Description","Kind");
        var outstanding=new ReadableTableBuilder(source,budget,"Reconciliation outstanding cash","ReconciliationId","BusinessId","JournalLineId","Cents");
        var corrections=new ReadableTableBuilder(source,budget,"Reconciliation correction choices","ReconciliationId","BusinessId","MovementId","Date","SignedCents","Description","Selected");
        var dependencies=new ReadableTableBuilder(source,budget,"Reconciliation dependencies","ReconciliationId","BusinessId","PriorReconciliationId");
        var carried=new ReadableTableBuilder(source,budget,"Reconciliation carried errors","ReconciliationId","BusinessId","Phase","ErrorId","BankLineId","Date","SignedCents","RemainingCents");
        var claims=new ReadableTableBuilder(source,budget,"Reconciliation bank error claims","ReconciliationId","BusinessId","BankLineId","Cents");
        var warnings=new ReadableTableBuilder(source,budget,"Reconciliation recorded warnings","ReconciliationId","BusinessId","Kind","Value");
        foreach(var item in Snapshots(db,source))
        {
            var value=item.Value;var observed=value.Sources;
            balances.Add(item.Id,source.BusinessId,observed.Mode,observed.Watermark,observed.GeneratedAtUtc,observed.OpeningConfirmed,observed.Statement.ClosingCents,observed.LedgerClosingCents,value.OutstandingDepositsCents,value.OutstandingPaymentsCents,value.PostedBookCorrectionsCents,value.LedgerBeforeCorrectionsCents,value.StatementErrorAdjustmentCents,value.AdjustedStatementCents,value.DifferenceCents,value.ReadyToComplete);
            AddPositions(positions,item.Id,source.BusinessId,"Bank",observed.Bank);AddPositions(positions,item.Id,source.BusinessId,"Cash",observed.Cash);
            foreach(var row in value.Draft.Outstanding.OrderBy(r=>r.JournalLineId))outstanding.Add(item.Id,source.BusinessId,row.JournalLineId,row.Cents);
            foreach(var row in observed.CorrectionChoices.OrderBy(r=>r.MovementId))corrections.Add(item.Id,source.BusinessId,row.MovementId,row.Date,row.SignedCents,row.Description,value.Draft.PostedCorrectionIds.Contains(row.MovementId));
            foreach(string prior in observed.PriorReconciliationIds.Order())dependencies.Add(item.Id,source.BusinessId,prior);
            AddCarried(carried,item.Id,source.BusinessId,"Observed",observed.CarriedErrors);AddCarried(carried,item.Id,source.BusinessId,"Remaining",value.RemainingStatementErrors);
            foreach(var claim in value.BankErrorClaims.OrderBy(r=>r.Key))claims.Add(item.Id,source.BusinessId,claim.Key,claim.Value);
            foreach(string warning in observed.ContinuityWarnings)warnings.Add(item.Id,source.BusinessId,"Continuity",warning);
            foreach(string id in value.UnexplainedCashIds.Order())warnings.Add(item.Id,source.BusinessId,"UnexplainedCashId",id);
        }
        return new[]{balances.Freeze(),positions.Freeze(),outstanding.Freeze(),corrections.Freeze(),dependencies.Freeze(),carried.Freeze(),claims.Freeze(),warnings.Freeze()}.Concat(Relational(db,source,budget)).ToArray();
    });

    private static List<(string Id,ReconciliationPreview Value)> Snapshots(DbWork db,ReadableExportSource source)=>db.Read(
        "SELECT Id,AccountId,StatementId,AsOfDate,SnapshotJson FROM Reconciliations WHERE BusinessId=$business ORDER BY AsOfDate,AtUtc,Id;",reader=>
        {
            ReconciliationPreview value;
            try{value=JsonSerializer.Deserialize<ReconciliationPreview>(reader.GetString(4))??throw new JsonException();}
            catch(JsonException){throw new InvalidDataException("A retained reconciliation cannot be projected.");}
            if(value.Sources.Statement.Id!=reader.GetString(2)||value.Draft.StatementId!=reader.GetString(2)||value.Sources.Statement.AccountId!=reader.GetString(1)||value.Sources.Statement.Through.ToString("yyyy-MM-dd")!=reader.GetString(3))throw new InvalidDataException("A retained reconciliation does not match its relational identity.");
            return(reader.GetString(0),value);
        },("$business",source.BusinessId));

    private static void AddPositions(ReadableTableBuilder table,string id,string business,string side,IEnumerable<ReconciliationLine> rows)
    {
        foreach(var row in rows.OrderBy(r=>r.Id))table.Add(id,business,side,row.Id,row.Date,row.SignedCents,row.RemainingCents,row.Description,row.Kind);
    }
    private static void AddCarried(ReadableTableBuilder table,string id,string business,string phase,IEnumerable<CarriedStatementError> rows)
    {
        foreach(var row in rows.OrderBy(r=>r.Id))table.Add(id,business,phase,row.Id,row.BankLineId,row.Date,row.SignedCents,row.RemainingCents);
    }
    private static ReadableExportTable[] Relational(DbWork db,ReadableExportSource source,ReadableExportBudget budget)=>[
        ReadableSqlTables.Read(db,source,budget,"Reconciliation approvals",["ReconciliationId","BusinessId","AccountId","StatementId","AsOfDate","RequestId","PayloadSha256","SnapshotSha256","ReviewedWatermark","ApprovedByOwnerId","ApprovedAtUtc","Reopened"],
            "SELECT C.Id,C.BusinessId,C.AccountId,C.StatementId,C.AsOfDate,C.RequestId,C.PayloadHash,C.SnapshotHash,C.ReviewedWatermark,C.ApprovedBy,C.AtUtc,CASE WHEN EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id) THEN 'true' ELSE 'false' END FROM Reconciliations C WHERE C.BusinessId=$business ORDER BY C.AsOfDate,C.AtUtc,C.Id;"),
        ReadableSqlTables.Read(db,source,budget,"Reconciliation bank coverage",["ReconciliationId","BusinessId","BankLineId"],
            "SELECT B.ReconciliationId,C.BusinessId,B.BankLineId FROM ReconciliationBankCoverage B JOIN Reconciliations C ON C.Id=B.ReconciliationId WHERE C.BusinessId=$business ORDER BY B.ReconciliationId,B.BankLineId;"),
        ReadableSqlTables.Read(db,source,budget,"Reconciliation statement errors",["ErrorId","BusinessId","ReconciliationId","BankLineId","Cents"],
            "SELECT E.Id,C.BusinessId,E.ReconciliationId,E.BankLineId,E.Cents FROM StatementErrors E JOIN Reconciliations C ON C.Id=E.ReconciliationId WHERE C.BusinessId=$business ORDER BY E.ReconciliationId,E.Id;"),
        ReadableSqlTables.Read(db,source,budget,"Reconciliation error resolutions",["ReconciliationId","BusinessId","ErrorId","BankLineId","Cents"],
            "SELECT E.ReconciliationId,C.BusinessId,E.ErrorId,E.BankLineId,E.Cents FROM StatementErrorResolutions E JOIN Reconciliations C ON C.Id=E.ReconciliationId WHERE C.BusinessId=$business ORDER BY E.ReconciliationId,E.ErrorId,E.BankLineId;"),
        ReadableSqlTables.Read(db,source,budget,"Reconciliation reopens",["ReopenId","BusinessId","ReconciliationId","RequestId","OwnerId","AtUtc"],
            "SELECT R.Id,C.BusinessId,R.ReconciliationId,R.RequestId,R.OwnerId,R.AtUtc FROM ReconciliationReopens R JOIN Reconciliations C ON C.Id=R.ReconciliationId WHERE C.BusinessId=$business ORDER BY R.AtUtc,R.Id;")];
}
