using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using System.Text.Json;

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
        bool reviewed=asset.Revision>0 && new AssetFacts(store).RequiresReview(owner,asset.Id);
        cost.IsReadOnly=reviewed;basis.IsReadOnly=reviewed;
        var use=Form.Input("Business-use fraction from 0 to 1 (blank means unknown)",asset.BusinessUseFraction?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"");
        var funding=Form.Select("Funding",["Undecided","Purchased","Contributed","Borrowed"],asset.Funding);
        var serial=Form.Input("Serial number",asset.SerialNumber);
        var evidence=Form.Input("Evidence reference",asset.EvidenceId);
        var archived=Archived(asset.Archived);
        var status=Form.Text();
        Asset Read()=>asset with{Name=name.Text,LegalOwner=legal.Text,AcquisitionDate=Input.Date(acquired.Text),InServiceDate=Input.Date(service.Text),
            CostCents=OptionalMoney(cost.Text),TaxBasisCents=OptionalMoney(basis.Text),BusinessUseFraction=string.IsNullOrWhiteSpace(use.Text)?null:Input.Decimal(use.Text),
            Funding=(string)funding.SelectedItem,SerialNumber=serial.Text,EvidenceId=evidence.Text.Trim(),Archived=archived.IsChecked==true};
        bool Changed(){try{return JsonSerializer.Serialize(Read())!=JsonSerializer.Serialize(asset);}catch{return true;}}
        _detail.Add(Form.Title("Asset"),Form.Text("Unknown cost and basis stay unknown. Saving an asset does not create a deduction, depreciation, or cash movement. Approve the actual business amount separately."),
            name,legal,acquired,service,cost,basis,use,funding,serial,evidence,archived,status,Form.Action("Save asset",status,async()=>
            {
                var value=Read();
                var saved=await Task.Run(()=>_book.SaveAsset(owner,value));
                Refresh();
                Edit(saved);
            }));
        if(reviewed)_detail.Add(Form.Text("Use Reviewed registered asset facts below to complete or correct cost and tax basis. This preserves the original facts and approved books."));
        if(asset.Revision>0)_detail.Add(new AssetFactView(store,owner,asset,Changed,result=>
        {
            if(_detail.Children.Contains(status) && !Changed()){Refresh();Edit(result);}
            else _status.Text="Asset fact review recorded. Reload its register entry when ready.";
        }).Build());
    }
}
