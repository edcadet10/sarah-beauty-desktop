using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Business.Exports;

internal static class ReadableDocumentTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var versions=new ReadableTableBuilder(source,budget,"Document versions","VersionId","DocumentId","BusinessId","BookingId","Kind","Number","Version","SourceDraftId","Mode","FrozenBusinessName","ClientName","BillingName","BillingAddress","Phone","Email","EventName","EventDate","Venue","ReadyByLocalTime","EventTimeZoneId","ReadyByUtcOffset","IssueDate","DueOrExpiryDate","Terms","RelatedDocument","NetCents","TaxCents","TotalCents","Status","IssuedByOwnerId","IssuedAtUtc","AcceptedDate","AcceptedByOwnerId","SnapshotSha256","RecordedPdfStatus","PdfRelativePath","RecordedPdfSha256");
        var lines=new ReadableTableBuilder(source,budget,"Document lines","VersionId","BusinessId","Sequence","ChargeId","Description","Unit","Quantity","UnitCents","DiscountCents","NetCents","TaxCents","TotalCents","RoundingCents");
        var milestones=new ReadableTableBuilder(source,budget,"Document milestones","VersionId","BusinessId","Sequence","Kind","Cents","DueDate");
        var receipts=new ReadableTableBuilder(source,budget,"Receipt snapshots","VersionId","BusinessId","PaymentDate","Method","ClientReference","RefundCents","PaymentAvailableCents","UnusedFundedCreditCents","CustomerInvoiceBalanceCents");
        var applications=new ReadableTableBuilder(source,budget,"Receipt applied amounts","VersionId","BusinessId","Sequence","InvoiceNumber","MilestoneKind","Cents");
        using var command=db.Command("""
            SELECT V.Id,D.Id,D.Type,D.Number,D.BookingId,V.Version,V.SourceDraftId,V.IssueDate,V.DataJson,V.SnapshotHash,V.Status,
            V.IssuedBy,V.IssuedAtUtc,V.AcceptedDate,V.AcceptedBy,A.Status,A.RelativePath,A.Sha256
            FROM DocumentVersions V JOIN Documents D ON D.Id=V.DocumentId LEFT JOIN DocumentArtifacts A ON A.VersionId=V.Id
            WHERE D.BusinessId=$business ORDER BY D.Id,V.Version,V.Id;
            """,("$business",source.BusinessId));using var reader=command.ExecuteReader();while(reader.Read())
        {
            var value=Snapshot(reader,source);string id=reader.GetString(0);
            versions.Add(id,reader.GetString(1),source.BusinessId,Text(reader,4),value.Kind,value.Number,value.Version,reader.GetString(6),value.Mode,value.BusinessName,value.Client.Name,value.Client.BillingName,value.Client.BillingAddress,value.Client.Phone,value.Client.Email,value.Event.Name,value.Event.Date,value.Event.Venue,value.Event.ReadyBy,value.Event.TimeZoneId,value.Event.ReadyByUtcOffset,value.IssueDate,value.DueDate,value.Terms,value.RelatedDocument,value.NetCents,value.TaxCents,value.TotalCents,reader.GetString(10),reader.GetString(11),reader.GetString(12),Text(reader,13),Text(reader,14),reader.GetString(9),Text(reader,15),Text(reader,16),Text(reader,17));
            for(int i=0;i<value.Lines.Length;i++){var line=value.Lines[i];lines.Add(id,source.BusinessId,i+1,line.ChargeId,line.Description,line.Unit,line.Quantity,line.UnitCents,line.DiscountCents,line.NetCents,line.TaxCents,line.TotalCents,line.RoundingCents);}
            for(int i=0;i<value.Milestones.Length;i++){var milestone=value.Milestones[i];milestones.Add(id,source.BusinessId,i+1,milestone.Kind,milestone.Cents,milestone.DueDate);}
            if(value.Receipt is ClientReceipt receipt)
            {
                receipts.Add(id,source.BusinessId,receipt.PaymentDate,receipt.Method,receipt.Reference,receipt.RefundCents,receipt.PaymentAvailableCents,receipt.UnusedFundedCreditCents,receipt.CustomerInvoiceBalanceCents);
                for(int i=0;i<receipt.Applications.Length;i++){var application=receipt.Applications[i];applications.Add(id,source.BusinessId,i+1,application.InvoiceNumber,application.Milestone,application.Cents);}
            }
        }
        return new[]{versions.Freeze(),lines.Freeze(),milestones.Freeze(),receipts.Freeze(),applications.Freeze()};
    });
    private static ClientSnapshot Snapshot(SqliteDataReader reader,ReadableExportSource source)
    {
        ClientSnapshot value;try{value=JsonSerializer.Deserialize<ClientSnapshot>(reader.GetString(8))??throw Invalid();}catch(JsonException){throw Invalid();}
        if(value.Kind.ToString()!=reader.GetString(2)||value.Number!=reader.GetString(3)||value.Version!=reader.GetInt64(5)||value.Mode!=source.Mode||value.IssueDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)!=reader.GetString(7)||value.Client is null||value.Event is null||value.Lines is null||value.Milestones is null)throw Invalid();return value;
    }
    private static string? Text(SqliteDataReader reader,int index)=>reader.IsDBNull(index)?null:reader.GetString(index);
    private static InvalidDataException Invalid()=>new("An issued document source does not match its readable export identity, mode, or supported fields.");
}
