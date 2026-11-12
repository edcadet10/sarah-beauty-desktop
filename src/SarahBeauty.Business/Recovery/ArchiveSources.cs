using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Business.Recovery;

internal sealed class ArchiveSources(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    internal ArchiveCapture Capture(OwnerSession owner,CancellationToken cancellation=default)
    {
        store.Read(db=>{store.RequireSession(db,owner);return true;});return Capture(owner.BusinessId,null,cancellation);
    }
    internal ArchiveCapture Capture(BackupPermit permit,CancellationToken cancellation=default)
    {
        var schedules=new BackupSchedules(store);store.Read(db=>{schedules.Require(db,permit);return true;});return Capture(permit.Schedule.BusinessId,db=>schedules.Require(db,permit),cancellation);
    }
    private ArchiveCapture Capture(string businessId,Action<DbWork>? authorizeSnapshot,CancellationToken cancellation)
    {
        string root=Path.GetDirectoryName(Path.GetFullPath(store.DatabasePath))!;var workspace=new ArchiveWorkspace(root);
        try
        {
            cancellation.ThrowIfCancellationRequested();string snapshot=workspace.FilePath("sarahbeauty.db");DatabaseBackup.CreateVerifiedSnapshotAt(store.DatabasePath,snapshot);FailurePoint?.Invoke("Database captured");
            cancellation.ThrowIfCancellationRequested();
            using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=snapshot,Mode=SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true}.ToString());connection.Open();var db=new DbWork(connection);
            authorizeSnapshot?.Invoke(db);var facts=ArchiveDatabaseChecks.Read(db);var header=Header(db,businessId,facts);var references=ArchiveReferences.Read(db,businessId);var files=new List<ArchiveFile>{Describe(snapshot,"sarahbeauty.db","Database")};
            if(references.Length>=100000)throw new InvalidDataException("The store exceeds the supported recovery file count.");
            long total=files[0].ByteLength;foreach(var reference in references)
            {
                cancellation.ThrowIfCancellationRequested();string path=ArchivePaths.Normalize(reference.RelativePath);string source=new ArchivePaths(root).Resolve(path),destination=workspace.FilePath(path);
                var file=Copy(source,destination,reference with{RelativePath=path},cancellation);total=checked(total+file.ByteLength);if(total>64L*1024*1024*1024)throw new InvalidDataException("The captured store exceeds the supported recovery size.");files.Add(file);FailurePoint?.Invoke("File copied");
            }
            cancellation.ThrowIfCancellationRequested();return new(workspace,header with{Files=files.ToArray()});
        }
        catch{workspace.Dispose();throw;}
    }
    private ArchiveManifest Header(DbWork db,string businessId,ArchiveDatabaseFacts facts)
    {
        StoreInstanceGuard.Require(db,store.StoreId,store.InstanceId,mode:store.Mode);
        string id=(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreId';")!,mode=(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!;
        int schema=Convert.ToInt32(db.Scalar("PRAGMA user_version;"));if(id!=store.StoreId || mode is not("TEST" or "LIVE") || schema!=BusinessSchema.Version)throw new InvalidDataException("The recovery snapshot identity, mode, or schema does not match its source.");
        string name=(string?)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",businessId))??throw new InvalidDataException("The recovery business was not found.");
        return new(1,businessId,name,id,mode,schema,(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",businessId))!,DateTimeOffset.UtcNow.ToString("O"),typeof(BusinessStore).Assembly.GetName().Version?.ToString()??"Unknown",[],facts);
    }
    private static ArchiveFile Describe(string file,string relative,string kind)
    {
        using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);return new(relative,kind,stream.Length,Convert.ToHexString(SHA256.HashData(stream)));
    }
    private static ArchiveFile Copy(string source,string destination,ArchiveFileReference reference,CancellationToken cancellation)
    {
        using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long total=0;int count;
        while((count=input.Read(buffer))>0){cancellation.ThrowIfCancellationRequested();total=checked(total+count);if(total>64L*1024*1024*1024)throw new InvalidDataException("A recovery source exceeds the supported size.");output.Write(buffer,0,count);hash.AppendData(buffer,0,count);}
        string actual=Convert.ToHexString(hash.GetHashAndReset());if(actual!=reference.ExpectedHash || reference.ExpectedBytes.HasValue && total!=reference.ExpectedBytes)throw new InvalidDataException("A required managed recovery file is missing or changed.");
        output.Flush(flushToDisk:true);return new(reference.RelativePath,reference.Kind,total,actual);
    }
}
