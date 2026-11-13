using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed record ClientDraftDiscardChoice(DocumentKind Kind,BusinessRecord Draft)
{
    internal string Label=>$"{KindName(Kind)} • {Draft.Name} • revision {Draft.Revision}";
    internal string Hash=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Draft,Draft.GetType()))));
    internal string Summary=>Label+"\n"+(Draft switch
    {
        DocumentDraft v=>$"Issue date: {Date(v.IssueDate)}\nDue date: {Date(v.DueDate)}\nTerms: {v.Terms}",
        InvoiceDraft v=>$"Issue date: {Date(v.IssueDate)}\nDue date: {Date(v.DueDate)}\nSelected charge portions: {v.Portions.Length}\nTerms: {v.Terms}",
        ReceiptDraft v=>$"Issue date: {Date(v.IssueDate)}\nClient reference: {v.ClientReference}",
        CreditNoteDraft v=>$"Issue date: {Date(v.IssueDate)}\nSelected charge portions: {v.Portions.Length}",
        _=>throw new ArgumentException("Choose a client document draft.")
    });
    internal static bool Available(BusinessRecord value)=>value switch{DocumentDraft v=>v.Status=="Draft"&&v.IssuedVersionId=="",InvoiceDraft v=>v.Status=="Draft"&&v.IssuedVersionId=="",ReceiptDraft v=>v.Status=="Draft"&&v.IssuedVersionId=="",CreditNoteDraft v=>v.Status=="Draft"&&v.IssuedVersionId=="",_=>false};
    internal static string KindName(DocumentKind kind)=>kind==DocumentKind.CreditNote?"Credit note":kind.ToString();
    internal static ClientDraftDiscardChoice Retained(ClientDraftDiscard value)
    {
        BusinessRecord? draft=value.Kind switch{DocumentKind.Quote=>JsonSerializer.Deserialize<DocumentDraft>(value.DraftSnapshotJson),DocumentKind.Invoice=>JsonSerializer.Deserialize<InvoiceDraft>(value.DraftSnapshotJson),DocumentKind.Receipt=>JsonSerializer.Deserialize<ReceiptDraft>(value.DraftSnapshotJson),DocumentKind.CreditNote=>JsonSerializer.Deserialize<CreditNoteDraft>(value.DraftSnapshotJson),_=>null};return new(value.Kind,draft??throw new InvalidDataException("The retained client draft is invalid."));
    }
    private static string Date(DateOnly? value)=>value?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"Not set";
}
