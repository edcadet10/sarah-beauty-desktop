using System;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Storage;

internal static class DatabaseMetadata
{
    internal static void Create(SqliteCommand command)
    {
        command.CommandText = """
            CREATE TABLE AppMetadata (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL
            );
            INSERT INTO AppMetadata (Key, Value) VALUES ('StoreId', $storeId);
            """;
        command.Parameters.AddWithValue("$storeId", Guid.NewGuid().ToString());
        command.ExecuteNonQuery();
        command.Parameters.Clear();
    }

    internal static string ReadStoreId(SqliteCommand command)
    {
        string? storeId = ReadValue(command, "StoreId");
        if (storeId is null || !Guid.TryParse(storeId, out Guid parsedId) || parsedId == Guid.Empty)
        {
            throw new InvalidOperationException("The database store ID is missing or invalid.");
        }

        return storeId;
    }

    internal static void ValidateMode(SqliteCommand command,int version,BusinessMode expected)
    {
        string? mode = ReadValue(command, "StoreMode");
        if (mode == BusinessModes.StoredName(expected) || (mode is null && version == 0 && expected==BusinessMode.Test))
        {
            return;
        }

        throw new InvalidOperationException(
            "This operation requires a "+BusinessModes.StoredName(expected)+" store. Its stored mode is missing or different.");
    }

    internal static void SaveMode(SqliteCommand command,BusinessMode mode)
    {
        command.CommandText = """
            INSERT INTO AppMetadata (Key, Value) VALUES ('StoreMode', $mode)
            ON CONFLICT(Key) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$mode",BusinessModes.StoredName(mode));command.ExecuteNonQuery();command.Parameters.Clear();
    }

    private static string? ReadValue(SqliteCommand command, string key)
    {
        command.CommandText = "SELECT Value FROM AppMetadata WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        object? value = command.ExecuteScalar();
        command.Parameters.Clear();
        if (value is not null && value is not string)
        {
            throw new InvalidOperationException($"The database metadata value for {key} must be text.");
        }

        return (string?)value;
    }
}
