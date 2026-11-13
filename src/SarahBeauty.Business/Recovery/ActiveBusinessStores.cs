using System.Text.Json;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record ActivatedBusinessStore(BusinessStore Store,OwnerSession Owner,ActiveStoreSelection Selection);
public sealed class ActiveBusinessStores(string parent,BusinessMode mode)
{
    private readonly RecoveredStorePaths _paths=new(parent,mode);
    internal Action<string>? FailurePoint {get;set;}
    public string ResolveDatabase()
    {
        var selection=ActiveStorePointer.Read(_paths);if(selection is null)return _paths.DefaultDatabase;string database=_paths.Database(selection.RequestId);
        using var connection=ReadOnly(database);var db=new DbWork(connection);
        if((string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreId';")!=selection.StoreId || (string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='WritableInstanceId';")!=selection.InstanceId || (string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!=BusinessModes.StoredName(mode))throw new InvalidDataException("The selected working-store identity changed. Preserve the copies and review recovery.");return database;
    }
    public async Task<ActivatedBusinessStore> ActivateAsync(string requestId,string ownerId,string passphrase,CancellationToken cancellation=default)
    {
        string database=_paths.Database(requestId);if(!File.Exists(database))throw new FileNotFoundException("The prepared recovered database is unavailable.");
        ArchivePaths.RejectLink(_paths.Lock);using var selectionLock=new FileStream(_paths.Lock,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        byte[]? before=ActiveStorePointer.ReadBytes(_paths.Pointer);string current=ResolveDatabase();string state;
        using(var connection=ReadOnly(database))state=(string?)new DbWork(connection).Scalar("SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';")??"Unknown";
        var store=new BusinessStore(database,mode,state=="Prepared"?"Prepared":"Active");var receipt=store.Read(db=>ReadyStoreChecks.Read(db,requestId));if(ownerId!=receipt.RecoveredOwnerId)throw new ArgumentException("Sign in as the owner whose access was reestablished for this copy.");
        var owner=await Task.Run(()=>new OwnerAccess(store).SignIn(ownerId,passphrase),cancellation);bool published=false,committed=false;byte[]? publishedBytes=null;
        try
        {
            FailurePoint?.Invoke("Owner authenticated");cancellation.ThrowIfCancellationRequested();
            if(state=="Active" && string.Equals(Path.GetFullPath(current),Path.GetFullPath(database),StringComparison.OrdinalIgnoreCase))return new(store,owner,ActiveStorePointer.Read(_paths)!);
            if(state!="Prepared")throw new InvalidDataException("This copy is already active outside this selection. Review that working store before continuing.");
            RequirePreviousRetired(current,database,receipt.StoreId);var selection=new ActiveStoreSelection(1,receipt.RequestId,store.StoreId,store.InstanceId,_paths.Relative(receipt.RequestId),DateTimeOffset.UtcNow.ToString("O"),BusinessModes.StoredName(mode));
            store.Transaction(db=>
            {
                store.RequireSession(db,owner);ReadyStoreChecks.Verify(db,Path.GetDirectoryName(database)!,ReadyStoreChecks.Read(db,requestId),cancellation,mode);FailurePoint?.Invoke("Prepared copy verified");RequirePreviousRetired(current,database,receipt.StoreId);
                db.Execute("UPDATE AppMetadata SET Value='Active' WHERE Key='WritableInstanceState';");BusinessStore.Audit(db,owner,"Activated recovered "+BusinessModes.StoredName(mode)+" working store",receipt.RequestId);
                cancellation.ThrowIfCancellationRequested();publishedBytes=JsonSerializer.SerializeToUtf8Bytes(selection);ActiveStorePointer.Write(_paths.Pointer,publishedBytes);published=true;FailurePoint?.Invoke("Selection published");cancellation.ThrowIfCancellationRequested();return true;
            });
            committed=true;FailurePoint?.Invoke("Selection committed");
            return new(new BusinessStore(database,mode),owner,selection);
        }
        catch(Exception error)
        {
            owner.Lock();if(committed)throw new IOException("The recovered working store was selected. Reopen the app to sign in to that store.",error);
            if(published)
            {
                var currentBytes=ActiveStorePointer.ReadBytes(_paths.Pointer);if(currentBytes is null || !currentBytes.SequenceEqual(publishedBytes!))throw new IOException("Selection changed during the interrupted activation. Existing store copies and the newer selection are preserved.",error);
                if(before is null)ActiveStorePointer.Clear(_paths.Pointer);else ActiveStorePointer.Write(_paths.Pointer,before);
            }
            throw;
        }
    }
    private void RequirePreviousRetired(string current,string candidate,string storeId)
    {
        if(!File.Exists(current)||string.Equals(Path.GetFullPath(current),Path.GetFullPath(candidate),StringComparison.OrdinalIgnoreCase))return;
        using var connection=ReadOnly(current);var db=new DbWork(connection);
        if((string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!=BusinessModes.StoredName(mode))throw new InvalidDataException("The previous primary belongs to a different mode. Preserve it and review recovery.");
        if(mode==BusinessMode.Live&&(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreId';")!=storeId)throw new ArgumentException("This archive belongs to a different LIVE business. Preserve the current business and review the archive separately.");
        if((string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreId';")==storeId && (string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';")!="Retired")throw new ArgumentException("Retire the previous local primary before activating another copy of the same business.");
    }
    private static SqliteConnection ReadOnly(string path)
    {
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());try{connection.Open();return connection;}catch{connection.Dispose();throw;}
    }
}
