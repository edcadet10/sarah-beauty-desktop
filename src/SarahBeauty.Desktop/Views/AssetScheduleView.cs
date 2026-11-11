using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class AssetScheduleView
{
    internal static UIElement Build(ReportSnapshot report)
    {
        if(report.Assets is null)return Form.Text("The asset schedule was not captured in this report version.");
        if(report.Assets.Version!=1)return Form.Text("This asset schedule version requires a compatible app version.");
        var schedule=report.Assets;var panel=Form.Panel();
        var known=schedule.Positions.Where(p=>p.CarryingCents.HasValue).ToArray();
        panel.Add(Form.Text($"Asset schedule version {schedule.Version} • Book values as of {report.Through:yyyy-MM-dd}\n"
            +$"Known carrying subtotal: {Money.Display(known.Sum(p=>p.CarryingCents!.Value))} • Assets with unknown book value: {schedule.Positions.Count-known.Length}"),
            Form.Text("Current register facts describe the records at capture time. Registered cost and tax basis do not replace approved book values."),
            PagedReport.Build("Asset book values",["Asset","Closing cost","Accumulated depreciation","Carrying value","Disposed as of date"],
                schedule.Positions.Select(p=>new[]{p.CurrentFacts.Name,Amount(p.ClosingCostCents),Amount(p.ClosingDepreciationCents),Amount(p.CarryingCents),p.DisposedAsOf?"Yes":"No"})),
            PagedReport.Build("Asset period movements",["Asset","Posted opening cost","Posted opening depreciation","Cost increases","Cost reductions","Depreciation added","Depreciation cleared"],
                schedule.Positions.Select(p=>new[]{p.CurrentFacts.Name,Amount(p.OpeningCostCents),Amount(p.OpeningDepreciationCents),Money.Display(p.CostIncreasesCents),Money.Display(p.CostReductionsCents),Money.Display(p.DepreciationAddedCents),Money.Display(p.DepreciationClearedCents)})),
            Form.Text("Increases include opening entries and reversals. Depreciation added/cleared includes disposal corrections; it does not calculate a tax deduction."));
        AddSourceReview(panel,schedule,report.From);return panel;
    }

    private static void AddSourceReview(Microsoft.UI.Xaml.Controls.StackPanel panel,AssetScheduleSnapshot schedule,DateOnly from)
    {
        var asset=Form.Records("Supporting asset",schedule.Positions.Select(p=>(p.CurrentFacts.AssetId,p.CurrentFacts.Name)));
        var details=Form.Panel();var status=Form.Text();
        panel.Add(asset,status,Form.Action("Show captured asset facts and sources",status,()=>
        {
            var position=schedule.Positions.SingleOrDefault(p=>p.CurrentFacts.AssetId==asset.SelectedId())??throw new ArgumentException("Choose an asset from this captured schedule.");
            details.Children.Clear();details.Add(Facts(position.CurrentFacts),Sources(schedule,position.CurrentFacts.AssetId,from));return Task.CompletedTask;
        }),details);
    }

    private static UIElement Facts(AssetRegisterFacts facts)=>ReportTable.Build(["Current register fact at capture","Value"],
        [["Asset",facts.Name],["Register revision",facts.Revision.ToString()],["Archived",facts.Archived?"Yes":"No"],
        ["Legal owner",Text(facts.LegalOwner)],["Acquisition date",facts.AcquisitionDate?.ToString("yyyy-MM-dd")??"Unknown"],
        ["In-service date",facts.InServiceDate?.ToString("yyyy-MM-dd")??"Unknown"],["Registered cost",Amount(facts.CostCents)],
        ["Registered tax basis",Amount(facts.TaxBasisCents)],["Business-use fraction",facts.BusinessUseFraction?.ToString("G29")??"Unknown"],
        ["Funding",Text(facts.Funding)],["Evidence",Text(facts.EvidenceId)]]);

    private static UIElement Sources(AssetScheduleSnapshot schedule,string asset,DateOnly from)
    {
        var panel=Form.Panel();
        panel.Add(Form.Text("Sources through the as-of date include earlier entries supporting the posted opening. Shared cash is shown only when the movement identifies this asset; no cash share is invented."),
            PagedReport.Build("Asset supporting entries",["Date / scope","Movement / description","Account","Debit","Credit","Book value line"],
                schedule.Entries.Where(e=>e.AssetId==asset).Select(e=>new[]{$"{e.Date:yyyy-MM-dd} • {(e.Date<from?"Before period":"In period")}",
                    $"{e.Kind} • {e.Memo} • {e.MovementId}",e.AccountCode+" • "+e.AccountName,Money.Display(e.DebitCents),Money.Display(e.CreditCents),e.IsBookValue?"Yes":"No"})),
            PagedReport.Build("Asset supporting events",["Date / scope","Kind / description","Movement identity"],
                schedule.Events.Where(e=>e.AssetId==asset).Select(e=>new[]{$"{e.Date:yyyy-MM-dd} • {(e.Date<from?"Before period":"In period")}",e.Kind+" • "+e.Memo,e.MovementId})));
        return panel;
    }

    private static string Amount(long? cents)=>cents.HasValue?Money.Display(cents.Value):"Unknown";
    private static string Text(string value)=>string.IsNullOrWhiteSpace(value)?"Unknown":value;
}
