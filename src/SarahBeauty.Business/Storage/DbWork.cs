using Microsoft.Data.Sqlite;

namespace SarahBeauty.Business.Storage;

public sealed class DbWork(SqliteConnection connection, SqliteTransaction? transaction = null)
{
    public SqliteCommand Command(string sql, params (string Key, object? Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (key, value) in values)
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }

    public int Execute(string sql, params (string Key, object? Value)[] values)
    {
        using var command = Command(sql, values);
        return command.ExecuteNonQuery();
    }

    public object? Scalar(string sql, params (string Key, object? Value)[] values)
    {
        using var command = Command(sql, values);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    public List<T> Read<T>(string sql, Func<SqliteDataReader, T> map,
        params (string Key, object? Value)[] values)
    {
        using var command = Command(sql, values);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }
}
