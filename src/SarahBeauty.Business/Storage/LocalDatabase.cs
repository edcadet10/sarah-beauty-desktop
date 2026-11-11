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
        InitializeTestDatabaseAt(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        int schemaVersion = DatabaseSchema.ReadSupportedVersion(command);
        bool isEmpty = schemaVersion == 0 && DatabaseSchema.IsEmpty(command);

        if (isEmpty)
        {
            DatabaseMetadata.Create(command);
        }
        else
        {
            DatabaseSchema.ValidateMetadataTable(command, schemaVersion < 2);
        }

        string storeId = DatabaseMetadata.ReadStoreId(command);
        DatabaseMetadata.ValidateTestMode(command, schemaVersion);
        schemaVersion = UpgradeIfNeeded(command, databasePath, schemaVersion, isEmpty);
        transaction.Commit();

        return (databasePath, storeId, schemaVersion);
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var options = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
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
        SqliteCommand command, string databasePath, int version, bool isEmpty)
    {
        if (version < BusinessSchema.Version)
        {
            // Keep the write reservation while a separate reader snapshots committed data.
            if (!isEmpty)
            {
                DatabaseBackup.CreateVerifiedSnapshot(databasePath);
            }

            if (version == 0) DatabaseMetadata.SaveTestMode(command);
            command.CommandText = BusinessSchema.Sql;
            command.ExecuteNonQuery();
            command.CommandText = $"PRAGMA user_version = {BusinessSchema.Version};";
            command.ExecuteNonQuery();
            return BusinessSchema.Version;
        }

        return version;
    }
}
