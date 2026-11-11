using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class PostingWriter
{
    internal static PostedMovement Save(DbWork db, OwnerSession owner, FinancialDraft draft, IReadOnlyList<JournalLine> lines, Action<string>? failure)
    {
        string id = Guid.NewGuid().ToString();
        string at = DateTimeOffset.UtcNow.ToString("O");
        string json = JsonSerializer.Serialize(draft);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
        db.Execute("""
            INSERT INTO PostingBatches (Id,BusinessId,DraftId,SourceRevision,PayloadHash,ApprovedBy,AtUtc)
            VALUES ($id,$business,$draft,$revision,$hash,$owner,$at);
            """, ("$id", id), ("$business", owner.BusinessId), ("$draft", draft.Id),
            ("$revision", draft.Revision), ("$hash", hash), ("$owner", owner.OwnerId), ("$at", at));
        failure?.Invoke("batch");
        SaveMovement(db, owner, draft, id, json);
        failure?.Invoke("movement");
        foreach (var line in lines)
        {
            db.Execute("INSERT INTO JournalLines (Id,MovementId,AccountId,DebitCents,CreditCents) VALUES ($id,$movement,$account,$debit,$credit);",
                ("$id", Guid.NewGuid().ToString()), ("$movement", id), ("$account", line.AccountId), ("$debit", line.DebitCents), ("$credit", line.CreditCents));
            failure?.Invoke("line");
        }
        return new(id, draft, owner.OwnerId, at);
    }

    private static void SaveMovement(DbWork db, OwnerSession owner, FinancialDraft draft, string id, string json) => db.Execute("""
        INSERT INTO Movements (Id,BusinessId,Kind,Date,Memo,GrossCents,TaxCents,CustomerId,OwnerId,BookingId,LoanId,AssetId,SupplierId,
            OriginalId,ReversesId,ReferenceNamespace,ExternalReference,DataJson)
        VALUES ($id,$business,$kind,$date,$memo,$gross,$tax,$customer,$owner,$booking,$loan,$asset,$supplier,
            $original,$reversal,$namespace,$reference,$json);
        """, ("$id", id), ("$business", owner.BusinessId), ("$kind", draft.Kind.ToString()),
        ("$date", draft.Date!.Value.ToString("yyyy-MM-dd")), ("$memo", draft.Name), ("$gross", draft.AmountCents), ("$tax", draft.TaxCents),
        ("$customer", Empty(draft.CustomerId)), ("$owner", Empty(draft.EconomicOwnerId)), ("$booking", Empty(draft.BookingId)),
        ("$loan", Empty(draft.LoanId)), ("$asset", Empty(draft.AssetId)), ("$supplier", Empty(draft.SupplierId)),
        ("$original", draft.Kind == FinancialKind.Reversal ? null : Empty(draft.OriginalId)),
        ("$reversal", draft.Kind == FinancialKind.Reversal ? Empty(draft.OriginalId) : null),
        ("$namespace", draft.ReliableReference ? draft.CashAccountId + ":" + draft.Provider.Trim().ToUpperInvariant() : null),
        ("$reference", draft.ReliableReference ? draft.ExternalReference.Trim() : null), ("$json", json));

    private static string? Empty(string value) => value.Length == 0 ? null : value;
}
