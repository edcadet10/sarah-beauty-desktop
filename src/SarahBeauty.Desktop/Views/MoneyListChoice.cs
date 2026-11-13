using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

[Microsoft.UI.Xaml.Data.Bindable]
internal sealed record MoneyListChoice(FinancialDraft Value)
{
    public string DisplayName=>string.IsNullOrWhiteSpace(Value.Name)?"Description missing":Value.Name;
    public string Summary=>$"{Value.Date?.ToString("yyyy-MM-dd")??"Date missing"} • {Value.Kind} • {Money.Display(Value.AmountCents)} • {Value.Status}";
    public string Reference=>"Draft reference: "+Value.Id;
    public override string ToString()=>Value+"\n"+Reference;
}
