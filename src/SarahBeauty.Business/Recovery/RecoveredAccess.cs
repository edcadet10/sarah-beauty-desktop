using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record RestoreCopyDecision(string OwnerId,string Reason,bool PreviousPrimaryAcknowledged);
public sealed record RecoveredStoreReceipt(int Version,string RequestId,string StoreId,string InstanceId,string Mode,string RecoveredOwnerId,string PreparedAtUtc,
    string SourceArchiveHash,string SourceManifestHash,long SourceWatermark,string? SourceInstanceId,string Reason,bool PreviousPrimaryAcknowledged,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]ArchiveManifest? SourceManifest=null);
public sealed record PreparedRecoveredStore(string Folder,RecoveredStoreReceipt Receipt)
{
    public string DatabasePath=>Path.Combine(Folder,"sarahbeauty.db");
}
internal static class RecoveredAccess
{
    internal static RecoveredStoreReceipt Prepare(string database,ArchiveManifest manifest,StagedArchiveReview review,RestoreCopyDecision decision,
        (string Salt,string Hash) credential,string request,CancellationToken cancellation)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false,ForeignKeys=true}.ToString());connection.Open();
        using var transaction=connection.BeginTransaction(deferred:false);var db=new DbWork(connection,transaction);
        if(db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",("$id",decision.OwnerId),("$business",manifest.BusinessId)) is null)throw new ArgumentException("Choose an active owner from the reviewed archive.");
        using(var command=db.Command("SELECT 1;"))SchemaMigrations.Apply(command,manifest.SchemaVersion);db.Execute($"PRAGMA user_version={BusinessSchema.Version};");
        string? priorInstance=db.Scalar("SELECT Value FROM AppMetadata WHERE Key='WritableInstanceId';") as string;
        string instance=Guid.NewGuid().ToString("N");var receipt=new RecoveredStoreReceipt(2,request,manifest.StoreId,instance,manifest.Mode,decision.OwnerId,DateTimeOffset.UtcNow.ToString("O"),review.ArchiveHash,review.ManifestHash,manifest.Watermark,manifest.SchemaVersion>=32?priorInstance:null,decision.Reason.Trim(),decision.PreviousPrimaryAcknowledged,manifest);
        db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES('WritableInstanceId',$instance) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value; INSERT INTO AppMetadata(Key,Value) VALUES('WritableInstanceState','Prepared') ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$instance",instance));
        db.Execute("UPDATE Owners SET Salt=$salt,Hash=$hash,Iterations=$iterations,FailedAttempts=0,LockedUntil=NULL WHERE Id=$id;",("$salt",credential.Salt),("$hash",credential.Hash),("$iterations",Passphrase.Iterations),("$id",decision.OwnerId));
        db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json);",("$key","RecoveredStore/"+request),("$json",JsonSerializer.Serialize(receipt)));
        BusinessStore.Audit(db,new OwnerSession(decision.OwnerId,"Recovered owner access",manifest.BusinessId,manifest.StoreId,instance,mode:BusinessModes.FromStoredName(manifest.Mode)),"Reestablished owner access from portable recovery",request);
        cancellation.ThrowIfCancellationRequested();transaction.Commit();return receipt;
    }
    internal static void Verify(string database,ArchiveManifest source,RecoveredStoreReceipt receipt,CancellationToken cancellation=default)
    {
        var mode=BusinessModes.FromStoredName(source.Mode);var store=new BusinessStore(database,mode,"Prepared");if(store.InstanceId!=receipt.InstanceId || store.StoreId!=source.StoreId||receipt.Mode!=source.Mode)throw new InvalidDataException("The recovered working copy identity did not match its preparation.");
        store.Read(db=>
        {
            ReadyStoreChecks.Verify(db,Path.GetDirectoryName(database)!,receipt,cancellation,mode);return true;
        });
    }
}
