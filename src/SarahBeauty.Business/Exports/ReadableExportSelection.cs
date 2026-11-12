namespace SarahBeauty.Business.Exports;

public sealed record ReadableExportSelection(DateOnly FinancialFrom,DateOnly FinancialThrough,DateOnly PlanningAsOf,DateOnly EventFrom,DateOnly EventThrough)
{
    internal void Validate()
    {
        if(!ValidWindow(FinancialFrom,FinancialThrough)||EventFrom<PlanningAsOf||!ValidWindow(EventFrom,EventThrough))
            throw new ArgumentException("Choose ordered financial and event windows of at most 120 calendar months, with events starting on or after the planning as-of date.");
    }

    private static bool ValidWindow(DateOnly from,DateOnly through)=>from<=through&&(through.Year-from.Year)*12+through.Month-from.Month+1<=120;
}
