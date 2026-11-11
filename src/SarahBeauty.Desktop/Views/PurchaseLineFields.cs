using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal static class PurchaseLineFields
{
    internal static void Edit(Microsoft.UI.Xaml.Controls.StackPanel panel,BusinessStore store,OwnerSession owner,PurchaseLine line,Action<PurchaseLine> saved)
    {
        panel.Children.Clear();
        var description=Form.Input("Line description",line.Description);
        var quantity=Form.Input("Quantity",line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var unit=Form.Input("Unit cost before tax in USD",Money.Input(line.UnitCostCents));
        var tax=Form.Input("Actual tax paid on this line (blank means unknown)",line.TaxPaidCents.HasValue?Money.Input(line.TaxPaidCents.Value):"");
        var use=Form.Input("Business-use fraction from 0 to 1 (blank means unreviewed)",line.BusinessUseFraction?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"");
        var account=Form.Records("Business expense or asset account",new ChartOfAccounts(store).List(owner).Where(a=>a.Active && a.CashKind=="None" && a.Type is "Expense" or "Asset").Select(a=>(a.Id,a.ToString())),line.AccountId);
        var asset=Form.Records("Individual asset (equipment lines require one)",new BusinessResources(store).Assets(owner).Select(a=>(a.Id,a.Name)),line.AssetId);
        var booking=Form.Records("Related booking (optional)",new BookingBook(store).List(owner).Select(b=>(b.Id,b.Name)),line.BookingId);
        var status=Form.Text();
        panel.Add(description,quantity,unit,tax,use,account,asset,booking,status,Form.Action("Keep this line in the draft",status,()=>
        {
            var value=line with{Description=description.Text,Quantity=Input.Decimal(quantity.Text),UnitCostCents=Money.Parse(unit.Text),
                TaxPaidCents=string.IsNullOrWhiteSpace(tax.Text)?null:Money.Parse(tax.Text),BusinessUseFraction=string.IsNullOrWhiteSpace(use.Text)?null:Input.Decimal(use.Text),
                AccountId=account.SelectedId(),AssetId=asset.SelectedId(),BookingId=booking.SelectedId()};
            saved(value);
            panel.Children.Clear();
            return Task.CompletedTask;
        }));
    }
}
