namespace SarahBeauty.Business.Finance;

public sealed record AssetValueChoice(string AssetId,string Treatment,long Generation=0)
{
    public const string Reduce="Reduce original cost";
    public const string Recover="Basis recovery";
    public const string Retire="Retire returned asset";
}
