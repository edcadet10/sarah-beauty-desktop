using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableLedgerTables
{
    private static readonly string[] FinancialColumns=["DraftId","Revision","Description","Kind","Date","GrossCents","TaxCents","TaxResolved","FeeCents","PrincipalCents","InterestCents","CashAccountId","CounterAccountId","ToAccountId","CustomerId","EconomicOwnerId","BookingId","LoanId","AssetId","SupplierId","OriginalMovementId","CreditId","PurchaseId","PurchaseFunding","SupplierRefundDestination","SupplierCreditTreatment","TaxRecoveryDestination","SourceTaxCents","Provider","PaymentMethod","ExternalReference","ReliableReference","OpeningCredit"];
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var accounts=ReadableSqlTables.Read(db,source,budget,"Accounts",["AccountId","BusinessId","Code","Name","Type","CashKind","Active","OpeningConfirmed"],"SELECT Id,BusinessId,Code,Name,Type,CashKind,CASE Active WHEN 1 THEN 'true' ELSE 'false' END,CASE OpeningConfirmed WHEN 1 THEN 'true' ELSE 'false' END FROM Accounts WHERE BusinessId=$business ORDER BY Code,Id;");
        var settings=ReadableSqlTables.Read(db,source,budget,"Book period settings",["BusinessId","ClosedThrough","OpeningConfirmed"],"SELECT BusinessId,ClosedThrough,CASE OpeningConfirmed WHEN 1 THEN 'true' ELSE 'false' END FROM BookSettings WHERE BusinessId=$business;");
        var journal=ReadableSqlTables.Read(db,source,budget,"Journal lines",["JournalLineId","BusinessId","MovementId","AccountId","DebitCents","CreditCents","AssetId"],"SELECT J.Id,M.BusinessId,J.MovementId,J.AccountId,J.DebitCents,J.CreditCents,J.AssetId FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId WHERE M.BusinessId=$business ORDER BY M.Date,M.Id,J.Id;");
        return new[]{accounts,settings,Drafts(db,source,budget),Posted(db,source,budget),journal};
    });
    private static ReadableExportTable Drafts(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Financial draft records",[..FinancialColumns,"BusinessId","Status","PostedBatchId","Archived","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId"]);
        ReadableRecords.ForEach<FinancialDraft>(db,source,"FinancialDraft",(value,audit)=>table.Add([..Values(value,posted:false),source.BusinessId,value.Status,value.PostedBatchId,value.Archived,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId]));return table.Freeze();
    }
    private static ReadableExportTable Posted(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Posted movements",[..FinancialColumns,"MovementId","BusinessId","ReversesMovementId","ApprovedByOwnerId","ApprovedAtUtc","PayloadSha256"]);
        using var command=db.Command("""
            SELECT M.Id,B.DraftId,B.SourceRevision,M.Kind,M.Date,M.GrossCents,M.TaxCents,M.ReversesId,M.DataJson,B.ApprovedBy,B.AtUtc,B.PayloadHash
            FROM Movements M JOIN PostingBatches B ON B.Id=M.Id WHERE M.BusinessId=$business AND B.BusinessId=$business ORDER BY M.Date,M.Id;
            """,("$business",source.BusinessId));using var reader=command.ExecuteReader();while(reader.Read())
        {
            FinancialDraft value;try{value=JsonSerializer.Deserialize<FinancialDraft>(reader.GetString(8))??throw Invalid();}catch(JsonException){throw Invalid();}
            if(value.Id!=reader.GetString(1)||value.Revision!=reader.GetInt64(2)||value.Kind.ToString()!=reader.GetString(3)||value.Date?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)!=reader.GetString(4)||value.AmountCents!=reader.GetInt64(5)||value.TaxCents!=reader.GetInt64(6))throw Invalid();
            table.Add([..Values(value,posted:true),reader.GetString(0),source.BusinessId,reader.IsDBNull(7)?null:reader.GetString(7),reader.GetString(9),reader.GetString(10),reader.GetString(11)]);
        }
        return table.Freeze();
    }
    private static object?[] Values(FinancialDraft value,bool posted)=>[value.Id,value.Revision,value.Name,value.Kind,value.Date,value.AmountCents,posted||value.TaxResolved?value.TaxCents:(long?)null,value.TaxResolved,value.FeeCents,value.PrincipalCents,value.InterestCents,value.CashAccountId,value.CounterAccountId,value.ToAccountId,value.CustomerId,value.EconomicOwnerId,value.BookingId,value.LoanId,value.AssetId,value.SupplierId,value.OriginalId,value.CreditId,value.PurchaseId,value.PurchaseFunding,value.SupplierRefundDestination,value.SupplierCreditTreatment,value.TaxRecoveryDestination,value.SourceTaxCents,value.Provider,value.PaymentMethod,value.ExternalReference,value.ReliableReference,value.OpeningCredit];
    private static InvalidDataException Invalid()=>new("An approved movement does not match its retained source identity, revision, date, or amounts.");
}
