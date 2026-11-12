using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class InvoiceVisibility
{
    internal static bool At(DbWork db,string business,DocumentVersion invoice,DateOnly date)
    {
        if(invoice.Snapshot.Kind!=DocumentKind.Invoice||invoice.Snapshot.IssueDate>date)return false;
        if(invoice.Status!="Void")return true;
        var decision=DocumentDecisionQueries.Read(db,business,invoice.Id).SingleOrDefault();
        if(decision is not{Action:DocumentDecisionAction.VoidInvoice})throw new InvalidDataException("The invoice's historical void date is unavailable. Review its retained decision before using this report.");
        return date<decision.DecisionDate;
    }
}
