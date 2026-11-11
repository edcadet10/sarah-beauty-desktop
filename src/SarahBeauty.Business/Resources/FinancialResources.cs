using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed record Loan : BusinessRecord
{
    public override string ToString()=>$"{Name} • {(Archived?"Archived":OpeningPrincipalCents.HasValue?"Opening specified":"Opening unknown")}";
    public DateOnly? OpeningDate { get; init; }
    public long? OpeningPrincipalCents { get; init; }
    public string Terms { get; init; } = "";
    public DateOnly? DueDate { get; init; }
    public string OwnerLenderId { get; init; } = "";
    public string EvidenceId { get; init; } = "";
}

public sealed record Asset : BusinessRecord
{
    public override string ToString()=>$"{Name} • {(Archived?"Archived":DisposedDate.HasValue?"Disposed":CostCents.HasValue?"Cost specified":"Cost unknown")}";
    public string LegalOwner { get; init; } = "";
    public DateOnly? AcquisitionDate { get; init; }
    public DateOnly? InServiceDate { get; init; }
    public long? CostCents { get; init; }
    public long? TaxBasisCents { get; init; }
    public decimal? BusinessUseFraction { get; init; }
    public string Funding { get; init; } = "Undecided";
    public string SerialNumber { get; init; } = "";
    public string EvidenceId { get; init; } = "";
    public DateOnly? DisposedDate { get; init; }
    public string DisposalMovementId {get;init;}="";
}

public sealed record Supplier : BusinessRecord
{
    public override string ToString()=>$"{Name} • {(Archived?"Archived":"Active")}";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string Address { get; init; } = "";
    public string Terms { get; init; } = "";
    public string PrivateNotes { get; init; } = "";
}
