using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class ClientDraftSnapshots
{
    internal static string RecordKind(DocumentKind kind)=>kind switch{DocumentKind.Quote=>"DocumentDraft",DocumentKind.Invoice=>"InvoiceDraft",DocumentKind.Receipt=>"ReceiptDraft",DocumentKind.CreditNote=>"CreditNoteDraft",_=>throw ClientDraftDiscardCodec.Invalid()};
    internal static string Json(BusinessRecord value)=>JsonSerializer.Serialize(value,value.GetType());
    internal static BusinessRecord Read(DocumentKind kind,string json)
    {
        try
        {
            BusinessRecord? value=kind switch{DocumentKind.Quote=>JsonSerializer.Deserialize<DocumentDraft>(json),DocumentKind.Invoice=>JsonSerializer.Deserialize<InvoiceDraft>(json),DocumentKind.Receipt=>JsonSerializer.Deserialize<ReceiptDraft>(json),DocumentKind.CreditNote=>JsonSerializer.Deserialize<CreditNoteDraft>(json),_=>throw ClientDraftDiscardCodec.Invalid()};
            if(value is null||value.Revision<1||string.IsNullOrWhiteSpace(value.Id)||Json(value)!=json||value is DocumentDraft{Kind:not DocumentKind.Quote})throw ClientDraftDiscardCodec.Invalid();return value;
        }
        catch(Exception error) when(error is JsonException or ArgumentException){throw new InvalidDataException("The retained client draft snapshot is invalid.",error);}
    }
    internal static string Status(BusinessRecord value)=>value switch{DocumentDraft v=>v.Status,InvoiceDraft v=>v.Status,ReceiptDraft v=>v.Status,CreditNoteDraft v=>v.Status,_=>throw ClientDraftDiscardCodec.Invalid()};
    internal static string IssuedVersion(BusinessRecord value)=>value switch{DocumentDraft v=>v.IssuedVersionId,InvoiceDraft v=>v.IssuedVersionId,ReceiptDraft v=>v.IssuedVersionId,CreditNoteDraft v=>v.IssuedVersionId,_=>throw ClientDraftDiscardCodec.Invalid()};
    internal static BusinessRecord Discarded(BusinessRecord value)
    {
        BusinessRecord discarded=value switch{DocumentDraft v=>v with{Status="Discarded"},InvoiceDraft v=>v with{Status="Discarded"},ReceiptDraft v=>v with{Status="Discarded"},CreditNoteDraft v=>v with{Status="Discarded"},_=>throw ClientDraftDiscardCodec.Invalid()};return discarded with{Revision=checked(value.Revision+1)};
    }
    internal static string Current(DbWork db,string business,DocumentKind kind,string id)
    {
        var rows=db.Read("SELECT Revision,DataJson FROM Records WHERE BusinessId=$business AND Kind=$kind AND Id=$id;",r=>(Revision:r.GetInt32(0),Json:r.GetString(1)),("$business",business),("$kind",RecordKind(kind)),("$id",id));
        if(rows.Count!=1)throw ClientDraftDiscardCodec.Invalid();var row=rows[0];var value=Read(kind,row.Json);if(value.Id!=id||value.Revision!=row.Revision)throw ClientDraftDiscardCodec.Invalid();return row.Json;
    }
}
