using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public enum FinancialKind
{
    Receipt, UnclassifiedReceipt, Expense, Transfer, Contribution, Withdrawal,
    LoanProceeds, LoanPayment, AssetPurchase, AssetContribution, Depreciation, AssetDisposal,
    OwnerPaidExpense, OwnerReimbursement, TaxRemittance, SupplierRefund, Refund, Reversal, Opening, PurchasePayment, PurchaseRefund, SupplierCreditUse, TaxRecovery, ReceiptClassification
}

public sealed record FinancialDraft : BusinessRecord
{
    public FinancialKind Kind { get; init; }
    public DateOnly? Date { get; init; }
    public long AmountCents { get; init; }
    public long TaxCents { get; init; }
    public bool TaxResolved { get; init; }
    public long FeeCents { get; init; }
    public long PrincipalCents { get; init; }
    public long InterestCents { get; init; }
    public string CashAccountId { get; init; } = "";
    public string CounterAccountId { get; init; } = "";
    public string ToAccountId { get; init; } = "";
    public string CustomerId { get; init; } = "";
    public string EconomicOwnerId { get; init; } = "";
    public string BookingId { get; init; } = "";
    public string LoanId { get; init; } = "";
    public LoanLenderSnapshot? LoanLender {get;init;}
    public string AssetId { get; init; } = "";
    public string SupplierId { get; init; } = "";
    public string OriginalId { get; init; } = "";
    public string CreditId { get; init; } = "";
    public string ExternalReference { get; init; } = "";
    public string Provider { get; init; } = "";
    public string PaymentMethod {get;init;}="";
    public bool ReliableReference { get; init; }
    public string EvidenceId { get; init; } = "";
    public string Status { get; init; } = "Draft";
    public string PostedBatchId { get; init; } = "";
    public bool OpeningCredit { get; init; }
    public long AssetGeneration {get;init;}
    public string PurchaseId {get;init;}="";
    public string PurchaseFunding {get;init;}="Business cash";
    public long PurchaseGeneration {get;init;}
    public string SupplierRefundDestination {get;init;}="Business cash";
    public long SourcePurchaseGeneration {get;init;}
    public string SupplierCreditTreatment {get;init;}="Original funding";
    public AssetValueChoice[] AssetAdjustments {get;init;}=[];
    public SarahBeauty.Business.Purchasing.SupplierRefundPortion[] SupplierRefundPortions {get;init;}=[];
    public TaxRefundAllocation? TaxRefundAllocation {get;init;}
    public string TaxRecoveryDestination {get;init;}=global::SarahBeauty.Business.Finance.TaxRecoveryDestination.Cash;
    public long TaxRecoveryGeneration {get;init;}
    public long SourcePaymentGeneration {get;init;}
    public long SourceTaxCents {get;init;}
    public override string ToString() => $"{Date?.ToString("yyyy-MM-dd") ?? "Date missing"} • {Kind} • {Money.Display(AmountCents)} • {Name} • {Status}";
}

public sealed record Account(string Id, string Code, string Name, string Type, string CashKind, bool Active, bool OpeningConfirmed)
{
    public override string ToString() => $"{Code} • {Name}";
}
public sealed record JournalLine(string AccountId, long DebitCents, long CreditCents,string AssetId="");
public sealed record PostedMovement(string Id, FinancialDraft Source, string ApprovedBy, string AtUtc);
