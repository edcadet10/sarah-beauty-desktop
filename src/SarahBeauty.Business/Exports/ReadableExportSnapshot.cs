using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Business.Exports;

public sealed record ReadableExportSource(int Version,string GenerationId,string BusinessId,string BusinessName,string StoreId,string InstanceId,string Mode,int SchemaVersion,long Watermark,string CapturedAtUtc,ArchiveDatabaseFacts Facts);
internal sealed class ReadableExportSnapshot:IDisposable
{
    private readonly ArchiveWorkspace _workspace;
    private readonly SqliteConnection _connection;
    private bool _disposed;
    internal ReadableExportSource Source {get;}
    internal ReadableExportSnapshot(ArchiveWorkspace workspace,SqliteConnection connection,ReadableExportSource source){_workspace=workspace;_connection=connection;Source=source;}
    internal T Read<T>(Func<DbWork,T> read){ObjectDisposedException.ThrowIf(_disposed,this);return read(new(_connection));}
    public void Dispose(){if(_disposed)return;_disposed=true;try{_connection.Dispose();}finally{_workspace.Dispose();}}
}
internal sealed class ReadableExportSnapshots(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    internal ReadableExportSnapshot Capture(OwnerSession owner,CancellationToken cancellation=default)=>Capture(owner.BusinessId,db=>store.RequireSession(db,owner),cancellation);
    internal ReadableExportSnapshot Capture(GoogleOutputPermit permit,CancellationToken cancellation=default)
    {
        if(permit.Kind!=GoogleOutputKind.SheetsExport)throw new UnauthorizedAccessException("Readable Sheets export permission is required.");return Capture(permit.Policy.BusinessId,db=>new GoogleOutputSettings(store).Require(db,permit),cancellation);
    }
    private ReadableExportSnapshot Capture(string business,Action<DbWork> authorize,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();store.Read(db=>{authorize(db);return true;});var workspace=new ArchiveWorkspace(Path.GetDirectoryName(Path.GetFullPath(store.DatabasePath))!);SqliteConnection? connection=null;
        try
        {
            string path=workspace.FilePath("sarahbeauty.db");DatabaseBackup.CreateVerifiedSnapshotAt(store.DatabasePath,path);FailurePoint?.Invoke("Database captured");cancellation.ThrowIfCancellationRequested();
            connection=new(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true}.ToString());connection.Open();var db=new DbWork(connection);StoreInstanceGuard.Require(db,store.StoreId,store.InstanceId);authorize(db);
            var facts=ArchiveDatabaseChecks.Read(db);int schema=Convert.ToInt32(db.Scalar("PRAGMA user_version;"));if(schema!=BusinessSchema.Version)throw new InvalidDataException("The readable export source schema is unsupported.");
            string name=(string?)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",business))??throw new InvalidDataException("The readable export business is unavailable.");string mode=(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!;long watermark=(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",business))!;
            cancellation.ThrowIfCancellationRequested();store.Read(current=>{authorize(current);return true;});return new(workspace,connection,new(1,Guid.NewGuid().ToString("N"),business,name,store.StoreId,store.InstanceId,mode,schema,watermark,DateTimeOffset.UtcNow.ToString("O"),facts));
        }
        catch{connection?.Dispose();workspace.Dispose();throw;}
    }
}
