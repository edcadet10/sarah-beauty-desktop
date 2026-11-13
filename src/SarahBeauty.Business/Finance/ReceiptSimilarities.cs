using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record SimilarReceipt(string MovementId,DateOnly Date,long GrossCents,string AccountName,string PayerName,string Description,
    string EvidenceId,string ApprovedByName,string ApprovedAtUtc,DateOnly? CancelledOn);
public sealed record ReceiptSimilarityReview(long Total,SimilarReceipt[] Receipts);

public sealed class ReceiptSimilarities(BusinessStore store)
{
    internal const int MaximumDetails=100;
    private const string Sources="""
        FROM Movements M JOIN PostingBatches B ON B.Id=M.Id JOIN Owners O ON O.Id=B.ApprovedBy
        LEFT JOIN Accounts A ON A.Id=json_extract(M.DataJson,'$.CashAccountId') LEFT JOIN Customers C ON C.Id=M.CustomerId
        LEFT JOIN Movements R ON R.ReversesId=M.Id
        WHERE M.BusinessId=$business AND M.Kind IN ('Receipt','UnclassifiedReceipt') AND M.Date=$date AND M.GrossCents=$amount
        AND M.Id<>$posted AND B.DraftId<>$draft
        """;

    public ReceiptSimilarityReview Find(OwnerSession owner,FinancialDraft draft)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        if(draft.Kind is not (FinancialKind.Receipt or FinancialKind.UnclassifiedReceipt) || draft.Date is null || draft.AmountCents<=0)
            return new ReceiptSimilarityReview(0,[]);
        var values=new (string,object?)[]{("$business",owner.BusinessId),("$date",draft.Date.Value.ToStorageDate()),("$amount",draft.AmountCents),("$posted",draft.PostedBatchId),("$draft",draft.Id)};
        long total=Convert.ToInt64(db.Scalar("SELECT COUNT(*) "+Sources+";",values));
        var details=db.Read("""
            SELECT M.Id,M.Date,M.GrossCents,COALESCE(A.Name,'Account unavailable'),COALESCE(json_extract(C.DataJson,'$.FullName'),'Payer unknown'),
                M.Memo,COALESCE(json_extract(M.DataJson,'$.EvidenceId'),''),O.Name,B.AtUtc,R.Date
            """+" "+Sources+$" ORDER BY B.AtUtc,M.Id LIMIT {MaximumDetails};",r=>new SimilarReceipt(r.GetString(0),StoredDates.Parse(r.GetString(1)),r.GetInt64(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetString(8),r.IsDBNull(9)?null:StoredDates.Parse(r.GetString(9))),values);
        return new ReceiptSimilarityReview(total,details.ToArray());
    });
}
