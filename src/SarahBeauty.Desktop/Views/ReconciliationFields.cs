using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReconciliationFields(ReconciliationSources sources)
{
    private readonly ReconciliationOutstanding _outstanding=new(sources.Cash.Where(c=>c.RemainingCents>0).ToArray());
    private readonly ListView _corrections=new(){SelectionMode=ListViewSelectionMode.Multiple,MaxHeight=200};

    internal UIElement Build()
    {
        var panel=Form.Panel();
        _corrections.ItemsSource=sources.CorrectionChoices.Select(c=>new Correction(c,$"{c.Date:yyyy-MM-dd} • {Money.Display(c.SignedCents)} • {c.Description} • {c.EvidenceId}")).ToArray();
        panel.Add(Form.Title("Support actual outstanding cash"),_outstanding.Build(),
            Form.Title("Link book corrections already posted"),Form.Text("Select only entries posted to correct this reconciliation. Their amounts already appear in the ledger closing balance."),_corrections);
        return panel;
    }

    internal ReconciliationDraft Read(string reason)=>new(sources.Statement.Id,reason)
    {Outstanding=_outstanding.Read(),PostedCorrectionIds=_corrections.SelectedItems.Cast<Correction>().Select(c=>c.Value.MovementId).ToArray()};

    private sealed record Correction(PostedBookCorrection Value,string Label){public override string ToString()=>Label;}
}
