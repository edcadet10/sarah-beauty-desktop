using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Business.Storage;

public sealed class BusinessStore
{
    public string DatabasePath { get; }
    public string StoreId { get; }

    public BusinessStore(string databasePath)
    {
        var database = SarahBeauty_Desktop.Storage.LocalDatabase.InitializeTestDatabaseAt(databasePath);
        (DatabasePath, StoreId) = (database.DatabasePath, database.StoreId);
    }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWrite,
            ForeignKeys = true, Pooling = false, DefaultTimeout = 5
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
        T value=read(new DbWork(connection,transaction));
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
        using var transaction = connection.BeginTransaction(deferred: false);
        T value = write(new DbWork(connection, transaction));
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
        if (session.StoreId != StoreId || session.ExpiresAt <= DateTimeOffset.UtcNow
            || db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
                ("$id", session.OwnerId), ("$business", session.BusinessId)) is null)
            throw new UnauthorizedAccessException("Sign in to this business to continue.");
        session.Touch();
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
