using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Purchasing;

internal sealed record SupplierFundingShare(long Gross,long Business,long Draw)
{
    internal long OutsidePersonal=>checked(Gross-Business-Draw);

    internal SupplierFundingShare Portion(long cents,long? businessOverride=null)
    {
        if(Gross<0 || Business<0 || Draw<0 || Business>Gross || Draw>Gross-Business || cents<0 || cents>Gross)
            throw new ArgumentException("Review the original gross, business, and personal funding components.");
        if(cents==0)return new(0,0,0);
        long business=businessOverride??Money.Proportion(Business,cents,Gross);
        if(business<Math.Max(0,cents-(Gross-Business)) || business>Math.Min(Business,cents))
            throw new ArgumentException("The business share exceeds its remaining original funding.");
        long left=cents-business;
        long draw=Math.Clamp(Money.Proportion(Draw,cents,Gross),Math.Max(0,left-OutsidePersonal),Math.Min(Draw,left));
        return new(cents,business,draw);
    }
}
