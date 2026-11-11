namespace SarahBeauty.Business.Purchasing;

public sealed record SupplierRefundPortion(string LineId,long GrossCents);
public sealed record SupplierRefundItem(string LineId,string Description,long RemainingCents,string AssetId);
