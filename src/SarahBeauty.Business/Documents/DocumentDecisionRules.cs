using SarahBeauty.Business.Attachments;

namespace SarahBeauty.Business.Documents;

public static class DocumentDecisionRules
{
    public static DocumentDecisionDraft Normalize(DocumentDecisionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);string reason=draft.Reason?.Trim()??"";
        if(!Enum.IsDefined(draft.Action) || draft.ExpectedStatus!="Issued" || draft.DecisionDate==default || reason.Length is <1 or >1000)
            throw new ArgumentException("Choose a current issued version, decision date, supported action, and reason of at most 1,000 characters.");
        CheckEvidence(draft.Evidence);
        return draft with{VersionId=Identity(draft.VersionId),DocumentSnapshotHash=Digest(draft.DocumentSnapshotHash),Reason=reason};
    }
    internal static string Result(DocumentDecisionAction action)=>action switch
    {
        DocumentDecisionAction.DeclineQuote=>"Declined",DocumentDecisionAction.WithdrawQuote=>"Withdrawn",
        DocumentDecisionAction.VoidQuote or DocumentDecisionAction.VoidInvoice=>"Void",_=>throw new ArgumentException("Choose a supported document decision.")
    };
    internal static DocumentKind Kind(DocumentDecisionAction action)=>action==DocumentDecisionAction.VoidInvoice?DocumentKind.Invoice:DocumentKind.Quote;
    internal static string Identity(string? value)=>Guid.TryParseExact(value,"D",out var id)&&id!=Guid.Empty?id.ToString():throw new ArgumentException("The document decision identity is invalid. Reload its source before continuing.");
    internal static string Digest(string? value)=>value is{Length:64}&&value.All(char.IsAsciiHexDigit)?value.ToUpperInvariant():throw new ArgumentException("The document source hash is invalid. Reload its saved version before continuing.");
    private static void CheckEvidence(ManagedEvidenceFile? value)
    {
        if(value is null)return;
        if(!Guid.TryParse(value.VersionId,out var id)||id==Guid.Empty || value.Number<1 || value.ByteLength<1 || string.IsNullOrWhiteSpace(value.Name) || string.IsNullOrWhiteSpace(value.OriginalFileName) ||
            value.Category is not("Receipt" or "Evidence") || value.MediaType is not("application/pdf" or "image/jpeg" or "image/png"))throw new ArgumentException("Choose a managed receipt or evidence version for this document decision.");
        _=Digest(value.MetadataHash);_=Digest(value.Sha256);
    }
}
