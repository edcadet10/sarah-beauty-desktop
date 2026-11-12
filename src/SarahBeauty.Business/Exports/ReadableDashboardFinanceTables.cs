using SarahBeauty.Business.Dashboard;

namespace SarahBeauty.Business.Exports;

internal static class ReadableDashboardFinanceTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var value=DashboardFinanceSources.Read(db,source.BusinessId,from,through,source.CapturedAtUtc);
        var scopes=new ReadableTableBuilder(source,budget,"Dashboard financial scope","ReportId","BusinessId","BusinessName","Mode","From","Through","ObservedWatermark","GeneratedAtUtc","Basis");
        scopes.Add(source.GenerationId,source.BusinessId,value.Reports.BusinessName,value.Reports.Mode,from,through,value.Reports.Watermark,value.Reports.GeneratedAtUtc,"Management cash basis with approved book adjustments");
        var months=new ReadableTableBuilder(source,budget,"Dashboard monthly financial data","ReportId","BusinessId","Month","IncomeCents","ExpenseCents","ResultCents","HasPostedActivity","HasClosedCoverage");
        foreach(var row in value.Months)months.Add(source.GenerationId,source.BusinessId,row.Month,row.IncomeCents,row.ExpenseCents,row.ResultCents,row.HasPostedActivity,row.HasClosedCoverage);
        var bands=new ReadableTableBuilder(source,budget,"Dashboard customer aging bands","ReportId","BusinessId","Band","Cents");
        var dues=new ReadableTableBuilder(source,budget,"Dashboard aging milestone sources","ReportId","BusinessId","Band","InvoiceVersionId","InvoiceNumber","CustomerId","FrozenCustomerName","MilestoneId","MilestoneKind","DueDate","Cents","TaxCents");
        foreach(var band in value.Aging)
        {
            bands.Add(source.GenerationId,source.BusinessId,band.Name,band.Cents);
            foreach(var row in band.Sources.OrderBy(r=>r.MilestoneId))dues.Add(source.GenerationId,source.BusinessId,band.Name,row.InvoiceVersionId,row.Number,row.CustomerId,row.CustomerName,row.MilestoneId,row.Kind,row.DueDate,row.Cents,row.TaxCents);
        }
        var expenses=new ReadableTableBuilder(source,budget,"Dashboard expense category data","ReportId","BusinessId","AccountId","Code","Name","Cents");
        foreach(var row in value.Expenses.OrderBy(r=>r.Code))expenses.Add(source.GenerationId,source.BusinessId,row.AccountId,row.Code,row.Name,row.Cents);
        var cash=new ReadableTableBuilder(source,budget,"Dashboard cash account balances","ReportId","BusinessId","AccountId","Code","Name","CashKind","OpeningConfirmedAsOf","BalanceCents","ReconciliationId","ReconciledThrough","ReconciliationSha256");
        foreach(var row in value.CashAccounts.OrderBy(r=>r.Account.Code))cash.Add(source.GenerationId,source.BusinessId,row.Account.Id,row.Account.Code,row.Account.Name,row.Account.CashKind,row.Account.OpeningConfirmed,row.BalanceCents,row.ReconciliationId,row.ReconciledThrough,row.ReconciliationHash);
        var entries=new ReadableTableBuilder(source,budget,"Dashboard cash journal sources","ReportId","BusinessId","Sequence","MovementId","Date","Kind","Description","AccountId","AccountName","DebitCents","CreditCents");
        int sequence=0;
        foreach(var row in value.CashEntries)entries.Add(source.GenerationId,source.BusinessId,++sequence,row.MovementId,row.Date,row.Kind,row.Memo,row.AccountId,row.AccountName,row.DebitCents,row.CreditCents);
        var owners=new ReadableTableBuilder(source,budget,"Dashboard owner movement data","ReportId","BusinessId","OwnerId","NameAtCapture","ContributionsCents","DrawsCents","ReimbursementsCents","LoanProceedsCents","LoanPrincipalPaidCents","AttributedLoanPrincipalCents");
        foreach(var row in value.Reports.Owners!.Positions.OrderBy(r=>r.OwnerId))owners.Add(source.GenerationId,source.BusinessId,row.OwnerId,row.Name,row.ContributionsCents,row.DrawsCents,row.ReimbursementsCents,row.LoanProceedsCents,row.LoanPrincipalPaidCents,row.AttributedLoanPrincipalCents);
        return new[]{scopes.Freeze(),months.Freeze(),bands.Freeze(),dues.Freeze(),expenses.Freeze(),cash.Freeze(),entries.Freeze(),owners.Freeze()};
    });
}
