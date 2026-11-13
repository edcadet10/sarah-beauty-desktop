using System.Text;

namespace SarahBeauty.Business.Documents;

public static class ClientDraftDiscardRules
{
    public static ClientDraftDiscardRequest Normalize(ClientDraftDiscardRequest draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if(!Enum.IsDefined(draft.Kind)||string.IsNullOrWhiteSpace(draft.DraftId)||draft.ExpectedRevision is <1 or int.MaxValue)throw new ArgumentException("Choose a saved client draft and its current revision before discarding it.");
        if(draft.DraftSnapshotHash is not{Length:64} hash||!hash.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F'))throw new ArgumentException("Review the exact saved draft snapshot before discarding it.");
        return draft with{Reason=Reason(draft.Reason)};
    }
    private static string Reason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);string text=value.Trim();
        if(text.Length is <1 or >1000||text.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t')))throw new ArgumentException("Record a private discard reason of at most 1,000 characters without unsupported control characters.");
        try{new UTF8Encoding(false,true).GetByteCount(text);}catch(EncoderFallbackException){throw new ArgumentException("Use valid Unicode text for the private discard reason.");}return text;
    }
}
