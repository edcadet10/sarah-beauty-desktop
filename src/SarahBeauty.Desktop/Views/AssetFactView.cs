using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class AssetFactView(BusinessStore store,OwnerSession owner,Asset asset,Func<bool> changed,Action<Asset> saved)
{
    private Func<bool> _hasChanges=()=>false;
    internal bool HasUnsavedChanges=>_hasChanges();

    internal UIElement Build()
    {
        var panel=Form.Panel();var approval=Form.Panel();var status=Form.Text();
        var cost=Form.Input("Reviewed registered cost in USD (blank means unknown)",Value(asset.CostCents));
        var basis=Form.Input("Reviewed registered tax basis in USD (blank means unconfirmed)",Value(asset.TaxBasisCents));
        var reason=Form.Input("Reason for the asset fact review");var evidence=Form.Input("Asset fact review evidence");
        _hasChanges=()=>cost.Text!=Value(asset.CostCents)||basis.Text!=Value(asset.TaxBasisCents)||reason.Text.Length>0||evidence.Text.Length>0;
        AssetFactDraft Read()=>new(asset.Id,Amount(cost.Text),Amount(basis.Text),reason.Text,evidence.Text);
        panel.Add(Form.Text($"Current registered cost: {Display(asset.CostCents)} • Current tax basis: {Display(asset.TaxBasisCents)}\n"
            +"This reviews current register facts. Approved book cost, depreciation, cash, and tax filings remain separate."),cost,basis,reason,evidence,status,
            Form.Action("Review registered asset facts",status,()=>
            {
                RequireSaved();var draft=Read();string request=Guid.NewGuid().ToString();approval.Children.Clear();
                var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to approve asset facts");var error=Form.Text();
                approval.Add(Form.Title("Captured asset fact review"),Form.Text($"{asset.Name}\nRegistered cost: {Display(asset.CostCents)} → {Display(draft.CostCents)}\n"
                    +$"Registered tax basis: {Display(asset.TaxBasisCents)} → {Display(draft.TaxBasisCents)}\nReason: {draft.Reason}\nEvidence: {draft.EvidenceId}\nNo approved book amount changes."),phrase,error,
                    Form.Action("Approve registered asset facts",error,async()=>
                    {
                        string password=phrase.Password;phrase.Password="";RequireSaved();
                        if(Read()!=draft)throw new ArgumentException("The asset fact fields changed. Review them again before approval.");
                        var result=await Task.Run(()=>new AssetFacts(store).Review(owner,draft,asset.Revision,request,password));saved(result);
                    }));return Task.CompletedTask;
            }),approval,History());
        return new Expander{Header="Reviewed registered asset facts",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    private void RequireSaved(){if(changed())throw new ArgumentException("Save the current asset fields before reviewing its registered facts.");}
    private static long? Amount(string value)=>string.IsNullOrWhiteSpace(value)?null:Money.Parse(value);
    private static string Value(long? cents)=>cents.HasValue?Money.Input(cents.Value):"";
    private static string Display(long? cents)=>cents.HasValue?Money.Display(cents.Value):"Unknown";
    private UIElement History()
    {
        var names=new OwnerAccess(store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
        return PagedReport.Build("Registered asset fact history",["Review time / owner","Registered cost","Registered tax basis","Reason / evidence"],asset.FactHistory.Select(h=>new[]{
            h.AtUtc+" • "+names.GetValueOrDefault(h.OwnerId,h.OwnerId),$"{Display(h.PreviousCostCents)} → {Display(h.CostCents)}",
            $"{Display(h.PreviousTaxBasisCents)} → {Display(h.TaxBasisCents)}",h.Reason+"\n"+h.EvidenceId}));
    }
}
