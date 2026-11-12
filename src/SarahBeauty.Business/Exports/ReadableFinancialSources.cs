using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal sealed record ReadablePostedAudit(string MovementId,string? ReversesMovementId,string ApprovedByOwnerId,string ApprovedAtUtc,string PayloadSha256);
internal static class ReadableFinancialSources
{
    internal static void ForEachPosted(DbWork db,ReadableExportSource source,Action<FinancialDraft,ReadablePostedAudit> add)
    {
        using var command=db.Command("""
            SELECT M.Id,B.DraftId,B.SourceRevision,M.Kind,M.Date,M.GrossCents,M.TaxCents,M.ReversesId,M.DataJson,B.ApprovedBy,B.AtUtc,B.PayloadHash
            FROM Movements M JOIN PostingBatches B ON B.Id=M.Id WHERE M.BusinessId=$business AND B.BusinessId=$business ORDER BY M.Date,M.Id;
            """,("$business",source.BusinessId));
        using var reader=command.ExecuteReader();
        while(reader.Read())
        {
            FinancialDraft value;
            try{value=JsonSerializer.Deserialize<FinancialDraft>(reader.GetString(8))??throw Invalid();}
            catch(JsonException){throw Invalid();}
            if(value.Id!=reader.GetString(1)||value.Revision!=reader.GetInt64(2)||value.Kind.ToString()!=reader.GetString(3)||value.Date?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)!=reader.GetString(4)||value.AmountCents!=reader.GetInt64(5)||value.TaxCents!=reader.GetInt64(6))throw Invalid();
            add(value,new(reader.GetString(0),reader.IsDBNull(7)?null:reader.GetString(7),reader.GetString(9),reader.GetString(10),reader.GetString(11)));
        }
    }
    private static InvalidDataException Invalid()=>new("An approved movement does not match its retained source identity, revision, date, or amounts.");
}
