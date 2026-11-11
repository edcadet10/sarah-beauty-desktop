using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class AssetRefundFields(BusinessStore store,OwnerSession owner,FinancialDraft draft,ComboBox original)
{
    private readonly List<(string Id,ComboBox Treatment)> _choices=[];
    private readonly StackPanel _assets=Form.Panel();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var status=Form.Text();
        panel.Add(Form.Text("Review whether the refunded business asset value reduces cost, recovers more than the remaining book value, or accompanies the actual return of the asset. A return decision also retires the tracked asset. Only choose an adjustment for an asset included in the refund or source credit being used."),
            Form.Action("Load original asset treatment",status,()=>
            {
                var source=new FinanceBook(store).Posted(owner).SingleOrDefault(p=>p.Id==original.SelectedId())
                    ??throw new ArgumentException("Choose the original asset purchase first.");
                var ids=AssetIds(source.Source);
                _choices.Clear();_assets.Children.Clear();
                foreach(string id in ids)Add(id,AssetValueChoice.Reduce);
                return Task.CompletedTask;
            }),status,_assets);
        foreach(var choice in draft.AssetAdjustments)Add(choice.AssetId,choice.Treatment);
        original.SelectionChanged+=(_,_)=>{_choices.Clear();_assets.Children.Clear();};
        return new Expander{Header="Asset return or basis recovery",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    private string[] AssetIds(FinancialDraft source)
    {
        if(source.Kind==FinancialKind.AssetPurchase && source.AssetId.Length>0)return [source.AssetId];
        if(source.Kind is not (FinancialKind.PurchasePayment or FinancialKind.SupplierCreditUse))throw new ArgumentException("Choose an original payment containing an individual asset.");
        return new PurchaseBook(store).List(owner).Single(p=>p.Id==source.PurchaseId).Lines
            .Where(l=>l.AssetId.Length>0).Select(l=>l.AssetId).Distinct().ToArray();
    }

    private void Add(string id,string selected)
    {
        var asset=new BusinessResources(store).Assets(owner).SingleOrDefault(a=>a.Id==id)??throw new ArgumentException("Choose an asset from this business.");
        var choice=Form.Select(asset.Name,[AssetValueChoice.Reduce,AssetValueChoice.Recover,AssetValueChoice.Retire],selected);
        _choices.Add((id,choice));
        _assets.Add(choice);
    }

    internal FinancialDraft Read(FinancialDraft value)=>value.Kind is not (FinancialKind.SupplierRefund or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)?value:value with
    {AssetAdjustments=_choices.Where(c=>(string)c.Treatment.SelectedItem!=AssetValueChoice.Reduce).Select(c=>new AssetValueChoice(c.Id,(string)c.Treatment.SelectedItem)).ToArray()};
}
