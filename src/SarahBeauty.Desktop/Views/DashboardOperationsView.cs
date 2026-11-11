using Microsoft.UI.Xaml;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class DashboardOperationsView
{
    internal static UIElement Build(DashboardOperationsSnapshot snapshot,Action<string> navigate)
    {
        var panel=Form.Panel();var status=Form.Text();var work=Form.Records("Dashboard work item",snapshot.Work.Select((w,i)=>(i.ToString(),w.Kind+" • "+w.Name)));
        panel.Add(Form.Title("Work and supply reviews"),Form.Text($"Planning business date {snapshot.BusinessDate:yyyy-MM-dd} • Current records at capture. Opening tasks use the financial as-of date.\n"
            +"A review item identifies missing information or work awaiting a decision; its presence does not approve a transaction."),
            PagedReport.Build("Dashboard pending work",["Kind","Name","Record ID / revision","Review reasons","Related value","Section"],
                snapshot.Work.Select(w=>new[]{w.Kind,w.Name,w.RecordId+" • "+w.Revision,string.Join("; ",w.Reasons),w.RelatedCents.HasValue?Money.Display(w.RelatedCents.Value):"Not specified",w.Route})),
            work,status,Form.Action("Open selected work section",status,()=>
            {
                if(!int.TryParse(work.SelectedId(),out int index)||index<0||index>=snapshot.Work.Length)throw new ArgumentException("Choose a captured work item.");
                navigate(snapshot.Work[index].Route);return Task.CompletedTask;
            }),
            PagedReport.Build("Dashboard supply alerts",["Item / ID","Brand / shade","Current status","Location","Expiry","Alerts"],
                snapshot.Supplies.Where(s=>s.Low||s.Finished||s.ExpiryAlert).Select(s=>new[]{s.Name+" • "+s.Id,s.Brand+" • "+s.Shade,s.Status,s.Location,s.ExpiryDate?.ToString("yyyy-MM-dd")??"Unknown",
                    string.Join("; ",new[]{s.Low?"Low":"",s.Finished?"Finished":"",s.ExpiryAlert?"Expiry on/before planning date":""}.Where(v=>v.Length>0))})),
            Form.Text($"Active supplies: {snapshot.Supplies.Length} • Low: {snapshot.Supplies.Count(s=>s.Low)} • Finished: {snapshot.Supplies.Count(s=>s.Finished)} • Expiry on/before date: {snapshot.Supplies.Count(s=>s.ExpiryAlert)} • Unknown expiry: {snapshot.Supplies.Count(s=>s.ExpiryDate is null)}"),
            Form.Action("Open resources and supplies",status,()=>{navigate("Resources");return Task.CompletedTask;}));return panel;
    }
}
