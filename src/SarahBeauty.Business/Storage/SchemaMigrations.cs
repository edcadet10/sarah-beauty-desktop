using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Storage;

internal static class SchemaMigrations
{
    internal static void Apply(SqliteCommand command, int fromVersion)
    {
        (int Version, string Sql)[] migrations = [(2, BusinessSchema.Sql), (3, CustomerSchema.Sql),
            (4, RecordStoreSchema.Sql), (5, FinanceSchema.Sql)];
        foreach (var migration in migrations.Where(m => m.Version > fromVersion))
        {
            command.CommandText = migration.Sql;
            command.ExecuteNonQuery();
        }
    }
}
