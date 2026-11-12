using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal sealed record ReadableRecordAudit(string CreatedAtUtc,string CreatedByOwnerId,string UpdatedAtUtc,string UpdatedByOwnerId);
internal static class ReadableRecords
{
    internal static void ForEach<T>(DbWork db,ReadableExportSource source,string kind,Action<T,ReadableRecordAudit> project) where T:BusinessRecord
    {
        using var command=db.Command("SELECT Id,Revision,DataJson,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy FROM Records WHERE BusinessId=$business AND Kind=$kind ORDER BY Id;",("$business",source.BusinessId),("$kind",kind));using var reader=command.ExecuteReader();while(reader.Read())
        {
            T value;try{value=JsonSerializer.Deserialize<T>(reader.GetString(2))??throw Invalid();}catch(JsonException){throw Invalid();}
            if(value.Id!=reader.GetString(0)||value.Revision!=reader.GetInt64(1)||string.IsNullOrWhiteSpace(value.Name))throw Invalid();project(value,new(reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6)));
        }
    }
    private static InvalidDataException Invalid()=>new("A retained business record does not match its readable export identity or supported fields.");
}
