using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardDraftWork
{
    private static readonly Dictionary<string,string> Routes=new()
    {
        ["FinancialDraft"]="Money",["DocumentDraft"]="Quotes",["InvoiceDraft"]="Invoices",["ReceiptDraft"]="Receipts",["CreditNoteDraft"]="Credit notes",
        ["PaymentApplicationDraft"]="Apply payments",["CreditDraft"]="Customer credits",["CancellationDraft"]="Cancellation cases",["Purchase"]="Supplier bills"
    };
    internal static IEnumerable<DashboardWorkItem> Read(DbWork db,OwnerSession owner)=>db.Read("""
        SELECT Id,Kind,Revision,json_extract(DataJson,'$.Name') FROM Records WHERE BusinessId=$business
        AND json_extract(DataJson,'$.Status')='Draft' AND COALESCE(json_extract(DataJson,'$.Archived'),0)=0 ORDER BY UpdatedAtUtc,Id;
        """,r=>(Id:r.GetString(0),Kind:r.GetString(1),Revision:r.GetInt32(2),Name:r.IsDBNull(3)?"Unnamed draft":r.GetString(3)),("$business",owner.BusinessId))
        .Where(r=>Routes.ContainsKey(r.Kind)).Select(r=>new DashboardWorkItem("Draft approval",r.Id,r.Name,Routes[r.Kind],r.Revision,["Saved draft awaiting its reviewed decision."]));
}
