using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace SarahBeauty.Storage.Tests;

internal sealed class TestStore : IDisposable
{
    internal const string StoreId = "9a737a42-8baa-4e78-9449-641c2b1fa64a";
    internal string Folder { get; } = Path.Combine(
        Path.GetTempPath(), "SarahBeautyStorageTests", Guid.NewGuid().ToString("N"));
    internal string DatabasePath => Path.Combine(Folder, "sarahbeauty.db");
    internal string BackupFolder => Path.Combine(Folder, "Backups");

    internal TestStore() => Directory.CreateDirectory(Folder);

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString());
        connection.Open();
        return connection;
    }

    internal void CreateLegacy(int version = 0)
    {
        Execute("""
            CREATE TABLE AppMetadata (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL);
            INSERT INTO AppMetadata VALUES ('StoreId', '9a737a42-8baa-4e78-9449-641c2b1fa64a');
            INSERT INTO AppMetadata VALUES ('FictionalNote', 'Keep this example');
            """);
        Execute($"PRAGMA user_version = {version};");
    }

    internal void Execute(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    internal string Hash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(DatabasePath)));

    internal string[] Snapshots() => Directory.Exists(BackupFolder)
        ? Directory.GetFiles(BackupFolder, "*.db") : [];

    internal static object? Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"SarahBeautyStorageTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(Folder);
        if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe fixture cleanup path.");
        Directory.Delete(target,recursive:true);
    }
}
