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
            (8, ApplicationSchema.Sql),(9, CreditSchema.Sql),(10,CreditNoteSchema.Sql),(11,ReceiptSchema.Sql),(12,OpeningSchema.Sql),(13,JournalAssetSchema.Sql),
            (14,SarahBeauty.Business.Purchasing.PurchasePaymentSchema.Sql),(15,SarahBeauty.Business.Purchasing.PurchaseRefundSchema.Sql),
            (16,SarahBeauty.Business.Purchasing.PurchaseCreditSchema.Sql),(17,SarahBeauty.Business.Purchasing.SupplierFundingSchema.Sql),
            (18,SarahBeauty.Business.Purchasing.SupplierTransferSchema.Sql),(19,TaxRecoverySchema.Sql),(20,SarahBeauty.Business.Banking.StatementSchema.Sql),
            (21,SarahBeauty.Business.Banking.BankMatchSchema.Sql),(22,SarahBeauty.Business.Banking.StatementEvidenceSchema.Sql),
            (23,SarahBeauty.Business.Banking.ReconciliationSchema.Sql),(24,SarahBeauty.Business.Banking.StatementErrorSchema.Sql),
            (25,SarahBeauty.Business.Banking.StatementWithdrawalSchema.Sql)];
        foreach (var migration in migrations.Where(m => m.Version > fromVersion))
        {
            command.CommandText = migration.Sql;
            command.ExecuteNonQuery();
        }
    }
}
