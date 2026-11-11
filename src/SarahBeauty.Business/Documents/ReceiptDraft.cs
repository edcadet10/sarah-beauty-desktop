using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed record ReceiptDraft : BusinessRecord
{
    public string PaymentId {get;init;}="";
    public DateOnly? IssueDate {get;init;}
    public string ClientReference {get;init;}="";
    public long ReviewedWatermark {get;init;}
    public string Status {get;init;}="Draft";
    public string IssuedVersionId {get;init;}="";
}
