using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Customers;

namespace SarahBeauty.Business.Storage;

internal static class SchemaMigrations
{
    internal static void Apply(SqliteCommand command, int fromVersion)
    {
        (int Version, string Sql)[] migrations = [(2, BusinessSchema.Sql), (3, CustomerSchema.Sql)];
        foreach (var migration in migrations.Where(m => m.Version > fromVersion))
        {
            command.CommandText = migration.Sql;
            command.ExecuteNonQuery();
        }
    }
}
