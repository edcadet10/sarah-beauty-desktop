using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record PreparedStoreCandidate(string RequestId,string BusinessName,string OwnerId,string OwnerName,string InstanceId,long SourceWatermark,string PreparedAtUtc,string Status,bool CanActivate);
public sealed class PreparedStoreCatalog(string parent,BusinessMode mode=BusinessMode.Test)
{
    private readonly string _mode=BusinessModes.StoredName(mode);
    public IReadOnlyList<PreparedStoreCandidate> List()
    {
        var paths=new RecoveredStorePaths(parent,mode);ArchivePaths.RejectLink(paths.Root);string folder=Path.Combine(paths.Root,"RestoredStores");ArchivePaths.RejectLink(folder);if(!Directory.Exists(folder))return [];
        var results=new List<PreparedStoreCandidate>();foreach(string directory in Directory.EnumerateDirectories(folder))
        {
            if(!Guid.TryParseExact(Path.GetFileName(directory),"N",out var id))continue;
            try{results.Add(Read(paths.Database(id.ToString()),id.ToString()));}
            catch(Exception error) when(error is IOException or SqliteException or InvalidOperationException or UnauthorizedAccessException){results.Add(new(id.ToString(),"Unavailable recovered copy","","","",0,"","Preserve this copy and prepare from a verified archive",false));}
        }
        return results.OrderByDescending(r=>r.PreparedAtUtc,StringComparer.Ordinal).ToArray();
    }
    private PreparedStoreCandidate Read(string database,string request)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();var db=new DbWork(connection);
        return db.Read("""
            SELECT json_extract(Value,'$.Version'),json_extract(Value,'$.RecoveredOwnerId'),json_extract(Value,'$.InstanceId'),json_extract(Value,'$.SourceWatermark'),json_extract(Value,'$.PreparedAtUtc'),
                (SELECT DisplayName FROM Business LIMIT 1),(SELECT Name FROM Owners WHERE Id=json_extract(AppMetadata.Value,'$.RecoveredOwnerId')),
                (SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState'),(SELECT Value FROM AppMetadata WHERE Key='StoreMode')
            FROM AppMetadata WHERE Key=$key;
            """,r=>
            {
                int version=r.GetInt32(0);string state=r.GetString(7);bool supported=version==2 && r.GetString(8)==_mode && state is "Prepared" or "Active";
                return new PreparedStoreCandidate(request,r.GetString(5),r.GetString(1),r.GetString(6),r.GetString(2),r.GetInt64(3),r.GetString(4),supported?state:"Requires fresh preparation",supported);
            },("$key","RecoveredStore/"+request)).SingleOrDefault()??throw new InvalidDataException("Recovered-copy provenance is missing.");
    }
}
