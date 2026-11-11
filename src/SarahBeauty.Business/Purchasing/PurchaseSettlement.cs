namespace SarahBeauty.Business.Purchasing;

public sealed record PurchaseDue(string PurchaseId,string SupplierId,string Name,DateOnly PurchaseDate,DateOnly? DueDate,long Cents);
internal sealed record PurchaseSettlement(string Id,string MovementId,string PurchaseId,string LineId,DateOnly Date,long Cents,long BusinessCents,string? ReversesId,
    int Sign=1,string? RefundsId=null,long DrawCents=0,string? ConsumesId=null);
