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
            (25,SarahBeauty.Business.Banking.StatementWithdrawalSchema.Sql),(26,BookPeriodSchema.Sql),(27,SarahBeauty.Business.Bookings.CancellationSchema.Sql),
            (28,SarahBeauty.Business.Resources.TripPaymentSchema.Sql),(29,SarahBeauty.Business.Attachments.AttachmentSchema.Sql),(30,SarahBeauty.Business.Attachments.EvidenceLinkSchema.Sql),
            // Version 31 raises the minimum writer contract: older models must not discard or bypass pinned financial file evidence.
            (31,"SELECT 1;"),(32,StoreInstanceGuard.Migration),(33,SarahBeauty.Business.Ownership.OwnerInterestSchema.Sql),
            // Version 34 distinguishes complete ownership export coverage from earlier schema-33 projection jobs.
            (34,"SELECT 1;"),
            // Version 35 requires every human session to match the currently stored credential.
            (35,"SELECT 1;"),(36,DocumentDecisionSchema.Sql),
            // Version 37 requires dated invoice-void reads and retained dependency checks.
            (37,"SELECT 1;"),
            // Version 38 distinguishes complete decision-history projection from older jobs.
            (38,"SELECT 1;"),(39,SarahBeauty.Business.Setup.BusinessProfileSchema.Sql),(40,ClientDraftDiscardSchema.Sql)];
        foreach (var migration in migrations.Where(m => m.Version > fromVersion))
        {
            command.CommandText = migration.Sql;
            command.ExecuteNonQuery();
        }
    }
}
