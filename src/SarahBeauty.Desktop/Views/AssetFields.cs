using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView
{
    private void Edit(Asset asset)
    {
        _detail.Children.Clear();
        var name=Form.Input("Asset name",asset.Name);
        var legal=Form.Input("Legal owner / ownership evidence",asset.LegalOwner);
        var acquired=Form.Input("Acquisition date (optional)",asset.AcquisitionDate?.ToString("yyyy-MM-dd")??"");
        var service=Form.Input("In-service date (optional)",asset.InServiceDate?.ToString("yyyy-MM-dd")??"");
        var cost=Form.Input("Original cost in USD (blank means unknown)",asset.CostCents.HasValue?Money.Input(asset.CostCents.Value):"");
        var basis=Form.Input("Tax basis in USD (blank means unconfirmed)",asset.TaxBasisCents.HasValue?Money.Input(asset.TaxBasisCents.Value):"");
        var use=Form.Input("Business-use fraction from 0 to 1 (blank means unknown)",asset.BusinessUseFraction?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"");
        var funding=Form.Select("Funding",["Undecided","Purchased","Contributed","Borrowed"],asset.Funding);
        var serial=Form.Input("Serial number",asset.SerialNumber);
        var evidence=Form.Input("Evidence reference",asset.EvidenceId);
        var archived=Archived(asset.Archived);
        var status=Form.Text();
        _detail.Add(Form.Title("Asset"),Form.Text("Unknown cost and basis stay unknown. Saving an asset does not create a deduction, depreciation, or cash movement. Approve the actual business amount separately."),
            name,legal,acquired,service,cost,basis,use,funding,serial,evidence,archived,status,Form.Action("Save asset",status,async()=>
            {
                var value=asset with{Name=name.Text,LegalOwner=legal.Text,AcquisitionDate=Input.Date(acquired.Text),InServiceDate=Input.Date(service.Text),
                    CostCents=OptionalMoney(cost.Text),TaxBasisCents=OptionalMoney(basis.Text),BusinessUseFraction=string.IsNullOrWhiteSpace(use.Text)?null:Input.Decimal(use.Text),
                    Funding=(string)funding.SelectedItem,SerialNumber=serial.Text,EvidenceId=evidence.Text.Trim(),Archived=archived.IsChecked==true};
                var saved=await Task.Run(()=>_book.SaveAsset(owner,value));
                Refresh();
                Edit(saved);
            }));
    }
}
