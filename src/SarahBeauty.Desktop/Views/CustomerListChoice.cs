using SarahBeauty.Business.Customers;

namespace SarahBeauty_Desktop.Views;

internal sealed record CustomerListChoice(Customer Value)
{
    public override string ToString()=>string.Join("\n",new[]{Value.ToString(),Contact,"Customer reference: "+Value.Id}.Where(v=>!string.IsNullOrWhiteSpace(v)));
    private string Contact=>string.Join(" • ",new[]{Value.Phone,Value.Email}.Where(v=>!string.IsNullOrWhiteSpace(v)));
}
