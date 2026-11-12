using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableBookHistory
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var dependencies=new ReadableTableBuilder(source,budget,"Closed period reconciliations","PeriodId","BusinessId","ReconciliationId","AccountId","StatementId","AsOfDate","ApprovedByOwnerId","ApprovedAtUtc","ReopenedAtCapture");
        ForEachClosed(db,source,(id,value)=>
        {
            foreach(var row in value.Reconciliations.OrderBy(r=>r.Id))dependencies.Add(id,source.BusinessId,row.Id,row.AccountId,row.StatementId,row.AsOfDate,row.ApprovedBy,row.AtUtc,row.Reopened);
        });
        return new[]{
            ReadableSqlTables.Read(db,source,budget,"Opening balance history",["OpeningId","BusinessId","AccountId","DimensionKind","DimensionId","Date","Cents","Credit","MovementId","OwnerId","AtUtc","RevokedAtUtc","RevokedByOwnerId","ReversalMovementId"],
                "SELECT Id,BusinessId,AccountId,DimensionKind,DimensionId,Date,Cents,CASE Credit WHEN 1 THEN 'true' ELSE 'false' END,MovementId,OwnerId,AtUtc,RevokedAt,RevokedBy,ReversalMovementId FROM OpeningBalances WHERE BusinessId=$business ORDER BY Date,AtUtc,Id;"),
            ReadableSqlTables.Read(db,source,budget,"Book period close history",["PeriodId","BusinessId","From","Through","RequestId","PayloadSha256","SnapshotSha256","ApprovedByOwnerId","ApprovedAtUtc","Reopened","ObservedMode","ObservedWatermark","ReportGeneratedAtUtc"],
                "SELECT C.Id,C.BusinessId,C.FromDate,C.ThroughDate,C.RequestId,C.PayloadHash,C.SnapshotHash,C.ApprovedBy,C.AtUtc,CASE WHEN EXISTS(SELECT 1 FROM BookPeriodReopens R WHERE R.PeriodId=C.Id) THEN 'true' ELSE 'false' END,json_extract(C.SnapshotJson,'$.Reports.Mode'),json_extract(C.SnapshotJson,'$.Reports.Watermark'),json_extract(C.SnapshotJson,'$.Reports.GeneratedAtUtc') FROM BookPeriodCloses C WHERE C.BusinessId=$business ORDER BY C.ThroughDate,C.AtUtc,C.Id;"),
            ReadableSqlTables.Read(db,source,budget,"Book period reopen history",["ReopenId","BusinessId","PeriodId","RequestId","OwnerId","AtUtc"],
                "SELECT R.Id,C.BusinessId,R.PeriodId,R.RequestId,R.OwnerId,R.AtUtc FROM BookPeriodReopens R JOIN BookPeriodCloses C ON C.Id=R.PeriodId WHERE C.BusinessId=$business ORDER BY R.AtUtc,R.Id;"),dependencies.Freeze()};
    });

    internal static void ForEachClosed(DbWork db,ReadableExportSource source,Action<string,BookPeriodPreview> add)
    {
        using var command=db.Command("SELECT Id,FromDate,ThroughDate,SnapshotJson FROM BookPeriodCloses WHERE BusinessId=$business ORDER BY ThroughDate,AtUtc,Id;",("$business",source.BusinessId));
        using var reader=command.ExecuteReader();
        while(reader.Read())
        {
            BookPeriodPreview value;
            try{value=JsonSerializer.Deserialize<BookPeriodPreview>(reader.GetString(3))??throw new JsonException();}
            catch(JsonException){throw new InvalidDataException("A retained book period cannot be projected.");}
            if(value.Draft.From.ToStorageDate()!=reader.GetString(1)||value.Draft.Through.ToStorageDate()!=reader.GetString(2)||value.Reports.From!=value.Draft.From||value.Reports.Through!=value.Draft.Through||value.Reports.Mode!=source.Mode||value.Reports.Watermark<0||value.Reports.Watermark>source.Watermark)throw new InvalidDataException("A retained book report does not match its recorded period or source scope.");
            add(reader.GetString(0),value);
        }
    }
}
