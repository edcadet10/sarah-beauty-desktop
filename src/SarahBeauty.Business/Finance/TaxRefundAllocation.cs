namespace SarahBeauty.Business.Finance;

public sealed record TaxRefundAllocation(long LiabilityCents,long RecoverableCents,long NonrecoverableCents);

public static class TaxRecoveryDestination
{
    public const string Cash="Business cash";
    public const string Offset="Offset liability";
    public const string WriteOff="Write off recovery";
}
