using SarahBeauty.Business.Customers;

namespace SarahBeauty_Desktop.Views;

[Microsoft.UI.Xaml.Data.Bindable]
internal sealed record CustomerListChoice(Customer Value)
{
    public string DisplayName=>Value.FullName+(Value.Archived?" • Archived":"");
    public string ContactSummary=>Contact.Length>0?Contact:"Contact information missing";
    public string Reference=>"Customer reference: "+Value.Id;
    public override string ToString()=>string.Join("\n",new[]{Value.ToString(),Contact,"Customer reference: "+Value.Id}.Where(v=>!string.IsNullOrWhiteSpace(v)));
    private string Contact=>string.Join(" • ",new[]{Value.Phone,Value.Email}.Where(v=>!string.IsNullOrWhiteSpace(v)));
}
