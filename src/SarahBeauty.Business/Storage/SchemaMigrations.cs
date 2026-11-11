using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Business.Storage;

internal static class SchemaMigrations
{
    internal static void Apply(SqliteCommand command, int fromVersion)
    {
        (int Version, string Sql)[] migrations = [(2, BusinessSchema.Sql), (3, CustomerSchema.Sql),
            (4, RecordStoreSchema.Sql), (5, FinanceSchema.Sql), (6, DocumentSchema.Sql), (7, InvoiceSchema.Sql),
            (8, ApplicationSchema.Sql),(9, CreditSchema.Sql)];
        foreach (var migration in migrations.Where(m => m.Version > fromVersion))
        {
            command.CommandText = migration.Sql;
            command.ExecuteNonQuery();
        }
    }
}
