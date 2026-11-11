using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Attachments;

public sealed class EvidenceTargets(BusinessStore store)
{
    private static readonly Dictionary<EvidenceTargetKind,string> Records=new()
    {
        [EvidenceTargetKind.Booking]="Booking",[EvidenceTargetKind.MoneyDraft]="FinancialDraft",[EvidenceTargetKind.Supplier]="Supplier",[EvidenceTargetKind.Purchase]="Purchase",
        [EvidenceTargetKind.Asset]="Asset",[EvidenceTargetKind.Loan]="Loan",[EvidenceTargetKind.Supply]="Supply",[EvidenceTargetKind.Trip]="MileageTrip",
        [EvidenceTargetKind.TaxPeriod]="TaxPeriod",[EvidenceTargetKind.Artist]="Artist",[EvidenceTargetKind.Service]="Service",[EvidenceTargetKind.QuoteDraft]="DocumentDraft",
        [EvidenceTargetKind.InvoiceDraft]="InvoiceDraft",[EvidenceTargetKind.CreditNoteDraft]="CreditNoteDraft",[EvidenceTargetKind.ReceiptDraft]="ReceiptDraft",
        [EvidenceTargetKind.PaymentDraft]="PaymentApplicationDraft",[EvidenceTargetKind.CreditDraft]="CreditDraft",[EvidenceTargetKind.CancellationDraft]="CancellationDraft"
    };
    public List<EvidenceTarget> List(OwnerSession owner,EvidenceTargetKind kind)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return List(db,owner,kind);});
    internal static List<EvidenceTarget> List(DbWork db,OwnerSession owner,EvidenceTargetKind kind)
    {
        if(Records.TryGetValue(kind,out string? record))return db.Read("""
            SELECT Id,COALESCE(json_extract(DataJson,'$.Name'),'Unnamed record'),Revision,COALESCE(json_extract(DataJson,'$.Status'),''),COALESCE(json_extract(DataJson,'$.Archived'),0)
            FROM Records WHERE BusinessId=$business AND Kind=$kind ORDER BY UpdatedAtUtc DESC,Id;
            """,r=>new EvidenceTarget(kind,r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetString(3),r.GetInt32(4)==1),("$business",owner.BusinessId),("$kind",record));
        return kind switch
        {
            EvidenceTargetKind.Customer=>db.Read("SELECT Id,json_extract(DataJson,'$.FullName'),Revision,Archived FROM Customers WHERE BusinessId=$business ORDER BY SearchName,Id;",
                r=>new EvidenceTarget(kind,r.GetString(0),r.GetString(1),r.GetInt32(2),"Customer",r.GetInt32(3)==1),("$business",owner.BusinessId)),
            EvidenceTargetKind.PostedMoney=>db.Read("SELECT Id,Memo,Kind,Date FROM Movements WHERE BusinessId=$business ORDER BY Date DESC,Id;",
                r=>new EvidenceTarget(kind,r.GetString(0),r.GetString(1),0,"Approved "+r.GetString(2)+" • "+r.GetString(3),false),("$business",owner.BusinessId)),
            EvidenceTargetKind.IssuedDocument=>DocumentVersions.List(db,owner).Select(v=>new EvidenceTarget(kind,v.Id,v.Snapshot.Number+" v"+v.Snapshot.Version,0,v.Snapshot.Kind+" • "+v.Status,false)).ToList(),
            _=>throw new ArgumentException("Choose a supported evidence record type.")
        };
    }
    internal static EvidenceTarget Get(DbWork db,OwnerSession owner,EvidenceTargetKind kind,string id)=>List(db,owner,kind).SingleOrDefault(t=>t.Id==id)??throw new ArgumentException("The evidence target does not exist in this business and record type.");
    public static string Label(EvidenceTargetKind kind)=>kind switch
    {
        EvidenceTargetKind.MoneyDraft=>"Money entry",EvidenceTargetKind.PostedMoney=>"Approved money movement",EvidenceTargetKind.IssuedDocument=>"Issued document",
        EvidenceTargetKind.QuoteDraft=>"Quote draft",EvidenceTargetKind.InvoiceDraft=>"Invoice draft",EvidenceTargetKind.CreditNoteDraft=>"Credit note draft",
        EvidenceTargetKind.ReceiptDraft=>"Receipt draft",EvidenceTargetKind.PaymentDraft=>"Payment application draft",EvidenceTargetKind.CreditDraft=>"Customer credit draft",
        EvidenceTargetKind.CancellationDraft=>"Cancellation case",EvidenceTargetKind.TaxPeriod=>"Tax period",EvidenceTargetKind.Service=>"Catalog service",_=>kind.ToString()
    };
}
