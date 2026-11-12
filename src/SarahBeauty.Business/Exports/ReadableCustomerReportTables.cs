using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableCustomerReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;string current=source.GenerationId;
        var balances=new ReadableTableBuilder(source,budget,"Report customer balances","ReportId","BusinessId","CustomerId","NameAtCapture","OpenCents","OverdueCents","DueTodayCents","FutureCents","UnknownDueCents","UnallocatedRecognizedReceiptCents","HeldReceiptCents","UnusedFundedCreditCents");
        var dues=new ReadableTableBuilder(source,budget,"Report invoice amounts due","ReportId","BusinessId","InvoiceVersionId","InvoiceNumber","CustomerId","FrozenCustomerName","MilestoneId","MilestoneKind","DueDate","Cents","TaxCents");
        var values=new ReadableTableBuilder(source,budget,"Report available customer values","ReportId","BusinessId","ValueKind","ValueId","CustomerId","SourceDate","OriginalGrossCents","OriginalTaxCents","AvailableCents","AvailableTaxCents","PaymentId","CounterAccountId","SourceTreatment","AdditionalCashReceipt");
        var coverage=new ReadableTableBuilder(source,budget,"Customer report detail coverage","ReportId","BusinessId","AggregateBalances","InvoiceMilestoneDetails","PaymentAndCreditDetails");
        var versions=DocumentVersions.List(db,source.BusinessId);
        foreach(var row in CustomerPositions.Read(db,source.BusinessId,through,versions))AddBalance(balances,current,source.BusinessId,row);
        foreach(var version in versions.Where(v=>v.Snapshot.Kind==DocumentKind.Invoice&&v.Status!="Void").OrderBy(v=>v.Id))
            foreach(var row in ValueAvailability.Invoice(db,source.BusinessId,version.Id,through))dues.Add(current,source.BusinessId,row.InvoiceVersionId,row.Number,row.CustomerId,row.CustomerName,row.MilestoneId,row.Kind,row.DueDate,row.Cents,row.TaxCents);
        ReadableFinancialSources.ForEachPosted(db,source,(value,audit)=>
        {
            if(value.Date>through||value.Kind is not(FinancialKind.Receipt or FinancialKind.UnclassifiedReceipt or FinancialKind.ReceiptClassification))return;
            var available=ValueAvailability.Payment(db,source.BusinessId,audit.MovementId,through);
            values.Add(current,source.BusinessId,"Receipt",audit.MovementId,value.CustomerId,value.Date,value.AmountCents,value.TaxCents,available.Cents,available.TaxCents,audit.MovementId,value.CounterAccountId,value.Kind,value.Kind!=FinancialKind.ReceiptClassification);
        });
        foreach(var credit in CreditValues.List(db,source.BusinessId).Where(c=>c.IssueDate<=through))
        {
            var available=CreditValues.Available(db,source.BusinessId,credit.Id,through);
            values.Add(current,source.BusinessId,"FundedCredit",credit.Id,credit.CustomerId,credit.IssueDate,credit.IssuedCents,credit.IssuedTaxCents,available.Cents,available.TaxCents,credit.PaymentId,null,credit.SourceTreatment,false);
        }
        coverage.Add(current,source.BusinessId,"Calculated from captured source","Calculated from captured source","Calculated from captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>
        {
            foreach(var row in period.Reports.Customers)AddBalance(balances,id,source.BusinessId,row);
            coverage.Add(id,source.BusinessId,"Retained at close","Not retained","Not retained");
        });
        return new[]{balances.Freeze(),dues.Freeze(),values.Freeze(),coverage.Freeze()};
    });

    private static void AddBalance(ReadableTableBuilder table,string report,string business,CustomerPosition row)=>table.Add(report,business,row.CustomerId,row.Name,row.OpenCents,row.OverdueCents,row.DueTodayCents,row.FutureCents,row.UnknownDueCents,row.UnallocatedReceiptsCents,row.HeldReceiptsCents,row.UnusedCreditCents);
}
