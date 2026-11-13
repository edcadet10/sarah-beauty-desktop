using System;
using System.IO;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Storage;

public static class LocalDatabase
{
    public static (string DatabasePath, string StoreId, int SchemaVersion)
        InitializeTestDatabase()
    {
        string folderPath = AppDataPaths.EnsureTestFolderExists();
        return InitializeTestDatabaseAt(Path.Combine(folderPath, "sarahbeauty.db"));
    }

    internal static (string DatabasePath, string StoreId, int SchemaVersion)
        InitializeTestDatabaseAt(string databasePath)=>InitializeDatabaseAt(databasePath,BusinessMode.Test);

    internal static (string DatabasePath,string StoreId,int SchemaVersion) InitializeDatabaseAt(string databasePath,BusinessMode mode,bool allowCreate=true)
    {
        BusinessModes.StoredName(mode);using var connection = OpenConnection(databasePath,allowCreate);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        int schemaVersion = DatabaseSchema.ReadSupportedVersion(command);
        bool isEmpty = schemaVersion == 0 && DatabaseSchema.IsEmpty(command);
        if(isEmpty&&!allowCreate)throw new InvalidOperationException("Choose an initialized business store. An empty file cannot be opened as an existing business.");

        if (isEmpty)
        {
            DatabaseMetadata.Create(command);
            DatabaseMetadata.SaveMode(command,mode);
        }
        else
        {
            DatabaseSchema.ValidateMetadataTable(command, schemaVersion < 2);
        }

        string storeId = DatabaseMetadata.ReadStoreId(command);
        DatabaseMetadata.ValidateMode(command,schemaVersion,mode);
        schemaVersion = UpgradeIfNeeded(command,databasePath,schemaVersion,isEmpty,mode);
        transaction.Commit();

        return (databasePath, storeId, schemaVersion);
    }

    private static SqliteConnection OpenConnection(string databasePath,bool allowCreate)
    {
        var options = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = allowCreate?SqliteOpenMode.ReadWriteCreate:SqliteOpenMode.ReadWrite,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false
        };

        var connection = new SqliteConnection(options.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static int UpgradeIfNeeded(
        SqliteCommand command,string databasePath,int version,bool isEmpty,BusinessMode mode)
    {
        if (version < BusinessSchema.Version)
        {
            // Keep the write reservation while a separate reader snapshots committed data.
            if (!isEmpty)
            {
                DatabaseBackup.CreateVerifiedSnapshot(databasePath);
            }

            if (version == 0) DatabaseMetadata.SaveMode(command,mode);
            SchemaMigrations.Apply(command, version);
            command.CommandText = $"PRAGMA user_version = {BusinessSchema.Version};";
            command.ExecuteNonQuery();
            return BusinessSchema.Version;
        }

        return version;
    }
}
