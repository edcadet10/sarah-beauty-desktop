using System;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Storage;

internal static class DatabaseSchema
{
    private const int SupportedVersion = BusinessSchema.Version;

    internal static int ReadSupportedVersion(SqliteCommand command)
    {
        command.CommandText = "PRAGMA user_version;";
        int version = Convert.ToInt32(command.ExecuteScalar()
            ?? throw new InvalidOperationException("The database has no schema version."));

        if (version < 0 || version > SupportedVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {version} is unsupported. " +
                $"The latest version this app supports is {SupportedVersion}.");
        }

        return version;
    }

    internal static bool IsEmpty(SqliteCommand command)
    {
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*';
            """;
        return Convert.ToInt32(command.ExecuteScalar()) == 0;
    }

    internal static void ValidateMetadataTable(SqliteCommand command, bool metadataOnly = true)
    {
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_schema
            WHERE name NOT GLOB 'sqlite_*'
              AND NOT (name = 'AppMetadata' AND type = 'table');
            """;
        if (metadataOnly && Convert.ToInt32(command.ExecuteScalar()) != 0)
        {
            throw InvalidSchema();
        }

        command.CommandText = "PRAGMA table_xinfo(AppMetadata);";
        using var reader = command.ExecuteReader();
        if (!IsColumn(reader, "Key", 1) || !IsColumn(reader, "Value", 0) || reader.Read())
        {
            throw InvalidSchema();
        }
    }

    private static bool IsColumn(SqliteDataReader reader, string name, int primaryKey)
    {
        return reader.Read()
            && reader.GetString(1) == name
            && string.Equals(reader.GetString(2), "TEXT", StringComparison.OrdinalIgnoreCase)
            && reader.GetInt32(3) == 1
            && reader.GetInt32(5) == primaryKey;
    }

    private static InvalidOperationException InvalidSchema() =>
        new("This database does not match the expected metadata schema.");
}
