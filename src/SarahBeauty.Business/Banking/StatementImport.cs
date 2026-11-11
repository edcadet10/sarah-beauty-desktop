namespace SarahBeauty.Business.Banking;

public sealed record StatementDraft(string AccountId,DateOnly From,DateOnly Through,long? OpeningCents,long? ClosingCents,StatementSource Source);
public sealed record BankLine(string Id,string AccountId,DateOnly Date,long SignedCents,string Description,string Reference,bool ReliableReference);
public sealed record StatementRowReview(StatementRow Row,BankLine[] Candidates,bool SimilarInSource);
public sealed record StatementImportPreview(StatementDraft Draft,long Generation,StatementRowReview[] Rows,string ExistingImportId);
public sealed record StatementRowChoice(int SourceRow,string ExistingLineId="",string Reason="");
public sealed record StatementImport(string Id,string AccountId,DateOnly From,DateOnly Through,long OpeningCents,long ClosingCents,
    string ContentHash,string ApprovedBy,string AtUtc,int RowCount);
public sealed record StatementImportLine(int SourceRow,BankLine Line,StatementRow Source,string Reason);
