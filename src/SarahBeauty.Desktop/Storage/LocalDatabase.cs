using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SarahBeauty_Desktop.Storage;

public static class LocalDatabase
{
    public static (string DatabasePath, string StoreId, int SchemaVersion)
        InitializeTestDatabase()
    {
        string folderPath = AppDataPaths.EnsureTestFolderExists();
        string databasePath = Path.Combine(folderPath, "sarahbeauty.db");

        var options = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        };

        using var connection = new SqliteConnection(options.ToString());
        connection.Open();

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = "PRAGMA user_version;";

        const int supportedSchemaVersion = 0;

        int schemaVersion = Convert.ToInt32(
            command.ExecuteScalar()
            ?? throw new InvalidOperationException(
                "The database did not return a schema version."));

        if (schemaVersion < 0 || schemaVersion > supportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {schemaVersion} is unsupported. " +
                $"The latest version this app supports is {supportedSchemaVersion}.");
        }

        command.CommandText = """
              CREATE TABLE IF NOT EXISTS AppMetadata (
                  Key TEXT NOT NULL PRIMARY KEY,
                  Value TEXT NOT NULL
              );
              """;

        command.ExecuteNonQuery();

        command.CommandText = """
              INSERT INTO AppMetadata (Key, Value)
              VALUES ('StoreId', $storeId)
              ON CONFLICT(Key) DO NOTHING;
              """;

        command.Parameters.AddWithValue(
            "$storeId",
            Guid.NewGuid().ToString());

        command.ExecuteNonQuery();

        command.Parameters.Clear();
        command.CommandText =
            "SELECT Value FROM AppMetadata WHERE Key = 'StoreId';";

        string storeId = command.ExecuteScalar() as string
            ?? throw new InvalidOperationException(
                "The database has no store ID.");

        transaction.Commit();

        return (databasePath, storeId, schemaVersion);
    }
}