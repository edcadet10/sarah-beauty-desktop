using System.Text.Json;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableContactTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read<ReadableExportTable[]>(db=>[Business(snapshot.Source,budget),Owners(db,snapshot.Source,budget),Customers(db,snapshot.Source,budget),Permissions(db,snapshot.Source,budget)]);
    private static ReadableExportTable Business(ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Business","BusinessId","DisplayName","Mode","StoreId","InstanceId","SchemaVersion","Watermark","CapturedAtUtc");table.Add(source.BusinessId,source.BusinessName,source.Mode,source.StoreId,source.InstanceId,source.SchemaVersion,source.Watermark,source.CapturedAtUtc);return table.Freeze();
    }
    private static ReadableExportTable Owners(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Owners","OwnerId","BusinessId","DisplayName","Active");using var command=db.Command("SELECT Id,Name,Active FROM Owners WHERE BusinessId=$business ORDER BY Id;",("$business",source.BusinessId));using var reader=command.ExecuteReader();while(reader.Read())table.Add(reader.GetString(0),source.BusinessId,reader.GetString(1),reader.GetInt64(2)==1);return table.Freeze();
    }
    private static ReadableExportTable Customers(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Customers","CustomerId","BusinessId","Revision","FullName","Phone","Email","PreferredContact","BillingName","BillingAddress","Archived","ContactWarning","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        using var command=db.Command("SELECT Id,Revision,Archived,DataJson,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy FROM Customers WHERE BusinessId=$business ORDER BY Id;",("$business",source.BusinessId));using var reader=command.ExecuteReader();while(reader.Read())
        {
            Customer value;try{value=JsonSerializer.Deserialize<Customer>(reader.GetString(3))??throw Invalid();}catch(JsonException){throw Invalid();}
            if(value.Id!=reader.GetString(0)||value.Revision!=reader.GetInt64(1)||value.Archived!=(reader.GetInt64(2)==1)||string.IsNullOrWhiteSpace(value.FullName)||value.Phone is null||value.Email is null)throw Invalid();
            table.Add(value.Id,source.BusinessId,value.Revision,value.FullName,value.Phone,value.Email,value.PreferredContact,value.BillingName,value.BillingAddress,value.Archived,value.ContactWarning,reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7));
        }
        return table.Freeze();
    }
    private static ReadableExportTable Permissions(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Customer permission status","PermissionId","CustomerId","BusinessId","PermissionType","Status","AtUtc","RecordedByOwnerId");using var command=db.Command("SELECT Id,CustomerId,Status,AtUtc,OwnerId FROM CustomerPermissions WHERE BusinessId=$business ORDER BY Id;",("$business",source.BusinessId));using var reader=command.ExecuteReader();while(reader.Read())table.Add(reader.GetString(0),reader.GetString(1),source.BusinessId,"Photo",reader.GetString(2),reader.GetString(3),reader.GetString(4));return table.Freeze();
    }
    private static InvalidDataException Invalid()=>new("A customer source does not match its readable export identity or supported fields. Review the local record before exporting.");
}
