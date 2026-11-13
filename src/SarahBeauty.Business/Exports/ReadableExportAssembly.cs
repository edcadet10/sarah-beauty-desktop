namespace SarahBeauty.Business.Exports;

internal sealed record ReadableExportGroup(string Name,IReadOnlyList<string> Tables);
internal sealed record ReadableExportAssembly(ReadableExportSource Source,ReadableExportSelection Selection,IReadOnlyList<ReadableExportGroup> Groups,IReadOnlyList<ReadableExportTable> Tables)
{
    internal static ReadableExportAssembly Build(ReadableExportSnapshot snapshot,ReadableExportSelection selection,CancellationToken cancellation=default)
    {
        selection.Validate();cancellation.ThrowIfCancellationRequested();var budget=new ReadableExportBudget(cancellation:cancellation);
        var tables=new List<ReadableExportTable>();var groups=new List<ReadableExportGroup>();
        foreach(var group in Builders(snapshot,budget,selection))
        {
            cancellation.ThrowIfCancellationRequested();var projected=group.Build();
            if(projected.Length==0)throw new InvalidDataException("A required readable export group has no table schema.");
            groups.Add(new(group.Name,Array.AsReadOnly(projected.Select(t=>t.Name).ToArray())));tables.AddRange(projected);
        }
        var verified=ReadableTableVerification.CopyAll(snapshot.Source,tables,new(cancellation:cancellation));
        cancellation.ThrowIfCancellationRequested();return new(snapshot.Source,selection,groups.AsReadOnly(),verified);
    }

    private static (string Name,Func<ReadableExportTable[]> Build)[] Builders(ReadableExportSnapshot source,ReadableExportBudget budget,ReadableExportSelection dates)=>
    [
        ("Contacts",()=>ReadableContactTables.Build(source,budget)),
        ("Catalog and bookings",()=>ReadablePlanningTables.Build(source,budget)),
        ("Appointments and artists",()=>ReadableScheduleTables.Build(source,budget)),
        ("Documents",()=>ReadableDocumentTables.Build(source,budget)),
        ("Billing links",()=>ReadableBillingLinks.Build(source,budget)),
        ("Ledger",()=>ReadableLedgerTables.Build(source,budget)),
        ("Customer credits",()=>ReadableCreditTables.Build(source,budget)),
        ("Resources",()=>ReadableResourceTables.Build(source,budget)),
        ("Supplies and mileage",()=>ReadableOperationalTables.Build(source,budget)),
        ("Purchases",()=>ReadablePurchaseTables.Build(source,budget)),
        ("Banking",()=>ReadableBankTables.Build(source,budget)),
        ("Reconciliation",()=>ReadableReconciliationTables.Build(source,budget)),
        ("Tax filings",()=>ReadableTaxTables.Build(source,budget)),
        ("Evidence references",()=>ReadableEvidenceTables.Build(source,budget)),
        ("Cancellation decisions",()=>ReadableCancellationTables.Build(source,budget)),
        ("Financial detail",()=>ReadableFinancialDetails.Build(source,budget)),
        ("Book history",()=>ReadableBookHistory.Build(source,budget)),
        ("Ledger reports",()=>ReadableLedgerReportTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Customer reports",()=>ReadableCustomerReportTables.Build(source,budget,dates.FinancialThrough)),
        ("Supplier reports",()=>ReadableSupplierReportTables.Build(source,budget,dates.FinancialThrough)),
        ("Tax reports",()=>ReadableTaxReportTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Asset reports",()=>ReadableAssetReportTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Debt reports",()=>ReadableDebtReportTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Owner reports",()=>ReadableOwnerReportTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Booking financial reports",()=>ReadableBookingFinancialTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Booking billing reports",()=>ReadableBookingBillingTables.Build(source,budget,dates.FinancialThrough)),
        ("Booking planning reports",()=>ReadableBookingPlanningTables.Build(source,budget)),
        ("Dashboard finance",()=>ReadableDashboardFinanceTables.Build(source,budget,dates.FinancialFrom,dates.FinancialThrough)),
        ("Dashboard upcoming events",()=>ReadableDashboardUpcomingTables.Build(source,budget,dates.PlanningAsOf,dates.EventFrom,dates.EventThrough)),
        ("Ownership settings",()=>ReadableOwnershipTables.Build(source,budget,dates.PlanningAsOf))
    ];
}
