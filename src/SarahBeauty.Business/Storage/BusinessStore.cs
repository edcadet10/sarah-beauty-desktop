using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Business.Storage;

public sealed class BusinessStore
{
    public string DatabasePath { get; }
    public string StoreId { get; }
    public string InstanceId {get;}
    public BusinessMode Mode {get;}=BusinessMode.Test;
    private readonly string _requiredState;
    internal Action? BeforeTransactionReservation {get;set;}

    public BusinessStore(string databasePath):this(databasePath,"Active"){}
    internal BusinessStore(string databasePath,string requiredState)
    {
        if(requiredState is not("Active" or "Prepared"))throw new ArgumentException("Unsupported business-store access purpose.");_requiredState=requiredState;
        var database = SarahBeauty_Desktop.Storage.LocalDatabase.InitializeTestDatabaseAt(databasePath);
        (DatabasePath, StoreId) = (database.DatabasePath, database.StoreId);
        using var connection=Connect();InstanceId=StoreInstanceGuard.Read(new DbWork(connection),StoreId,requiredState:_requiredState);
    }

    internal SqliteConnection Open()
    {
        var connection=Connect();try{StoreInstanceGuard.Require(new DbWork(connection),StoreId,InstanceId,_requiredState);return connection;}
        catch{connection.Dispose();throw;}
    }

    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWrite,
            ForeignKeys = true, Pooling = false, DefaultTimeout = 30
        }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    internal T Read<T>(Func<DbWork, T> read)
    {
        using var connection = Open();
        return read(new DbWork(connection));
    }

    internal T ReadSnapshot<T>(Func<DbWork,T> read)
    {
        using var connection=Open();
        using var transaction=connection.BeginTransaction(deferred:true);
        var db=new DbWork(connection,transaction);StoreInstanceGuard.Require(db,StoreId,InstanceId,_requiredState);T value=read(db);
        transaction.Commit();
        return value;
    }

    public string BusinessName(OwnerSession session) => Read(db =>
    {
        RequireSession(db, session);
        return (string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;", ("$id", session.BusinessId))!;
    });

    internal T Transaction<T>(Func<DbWork, T> write)
    {
        using var connection = Open();
        BeforeTransactionReservation?.Invoke();
        using var transaction = connection.BeginTransaction(deferred: false);
        var db=new DbWork(connection,transaction);StoreInstanceGuard.Require(db,StoreId,InstanceId,_requiredState);T value = write(db);
        transaction.Commit();
        return value;
    }

    internal T Write<T>(OwnerSession session, string action, string? recordId, Func<DbWork, T> write) =>
        Transaction(db =>
        {
            RequireSession(db, session);
            T result = write(db);
            Audit(db, session, action, recordId);
            return result;
        });

    internal void RequireSession(DbWork db, OwnerSession session)
    {
        RequireCurrentSession(db,session);session.Touch();
    }

    internal void RequireCurrentSession(DbWork db,OwnerSession session)
    {
        StoreInstanceGuard.Require(db,StoreId,InstanceId,_requiredState);
        if (session.Mode!=Mode || session.StoreId != StoreId || session.InstanceId!=InstanceId || !session.IsActive)
            throw new UnauthorizedAccessException("Sign in to this business to continue.");
        OwnerCredentials.Require(db,session);
    }

    internal static void Audit(DbWork db, OwnerSession session, string action, string? recordId)
    {
        db.Execute("""
            INSERT INTO Audit (BusinessId,OwnerId,Action,RecordId,AtUtc)
            VALUES ($business,$owner,$action,$record,$at);
            UPDATE Business SET Watermark=Watermark+1 WHERE Id=$business;
            """, ("$business", session.BusinessId), ("$owner", session.OwnerId),
            ("$action", action), ("$record", recordId), ("$at", DateTimeOffset.UtcNow.ToString("O")));
    }
}
