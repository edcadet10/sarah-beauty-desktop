using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class DashboardView(BusinessStore store,OwnerSession owner,Action<string> navigate)
{
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _result=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly TextBox _from=Form.Input("Financial period start (YYYY-MM-DD)");
    private readonly TextBox _through=Form.Input("Financial as-of date (YYYY-MM-DD)");
    private readonly TextBox _planning=Form.Input("Planning business date (YYYY-MM-DD)");
    private readonly TextBox _upcomingFrom=Form.Input("Upcoming window start (YYYY-MM-DD)");
    private readonly TextBox _upcomingThrough=Form.Input("Upcoming window end (YYYY-MM-DD)");
    private bool _loading;
    private bool _initialized;

    internal UIElement Build()
    {
        var today=DateOnly.FromDateTime(DateTime.Today);_from.Text=new DateOnly(today.Year,1,1).ToString("yyyy-MM-dd");_through.Text=_planning.Text=_upcomingFrom.Text=today.ToString("yyyy-MM-dd");
        _upcomingThrough.Text=today.AddMonths(12).AddDays(-1).ToString("yyyy-MM-dd");var panel=Form.Panel();var dates=Form.Panel();
        dates.Add(_from,_through,_planning,_upcomingFrom,_upcomingThrough);
        panel.Add(Form.Title("Business dashboard"),Form.Muted("Your money, upcoming bookings, and work to review."),
            new Expander{Header="Dashboard dates",Content=dates,HorizontalAlignment=HorizontalAlignment.Stretch},Form.PrimaryAction("Refresh dashboard",_status,Refresh),_status,_result);
        foreach(var field in Fields())field.TextChanged+=(_,_)=>{if(_initialized)_status.Text="Date fields changed. Refresh to capture a new dashboard; the displayed dates still identify the previous result.";};
        panel.Loaded+=async(_,_)=>
        {
            if(_initialized)return;_initialized=true;
            try{await Refresh();}catch(Exception error){_status.Text=error.Message;}
        };
        return panel;
    }

    private TextBox[] Fields()=>[_from,_through,_planning,_upcomingFrom,_upcomingThrough];
    private string[] Values()=>Fields().Select(f=>f.Text).ToArray();
    private static DateOnly Date(string text)=>Input.Date(text)??throw new ArgumentException("Enter every dashboard date as YYYY-MM-DD.");

    private async Task Refresh()
    {
        if(_loading)throw new ArgumentException("The dashboard is loading. Wait for this capture before refreshing again.");
        var values=Values();var request=new DashboardRequest(Date(values[0]),Date(values[1]),Date(values[2]),Date(values[3]),Date(values[4]));
        _loading=true;_status.Text="Loading a consistent local snapshot…";
        try
        {
            var capture=await Task.Run(()=>(Snapshot:new DashboardSources(store).Load(owner,request),Backup:new LocalBackups(store).Health(owner)));var snapshot=capture.Snapshot;
            if(!values.SequenceEqual(Values())){_status.Text="Dates changed during loading. Refresh again to capture those dates.";return;}
            var view=Show(snapshot,capture.Backup);_result.Content=view;_status.Text="Dashboard refreshed. Refresh again to include later changes.";
        }
        finally{_loading=false;}
    }

    private UIElement Show(DashboardSnapshot snapshot,LocalBackupHealth backup)
    {
        var report=snapshot.Finance.Reports;var panel=Form.Panel();panel.Spacing=20;
        var work=Section("Work and supply reviews",DashboardOperationsView.Build(snapshot.Operations,navigate));
        var upcoming=Section("Upcoming booking details",DashboardUpcomingView.Build(snapshot.Upcoming,navigate));
        var capture=Form.Text($"{report.BusinessName} • {report.Mode} • Watermark {report.Watermark}\nCaptured {report.GeneratedAtUtc}\n"
            +$"Financial period {report.From:yyyy-MM-dd} through {report.Through:yyyy-MM-dd}\n"
            +$"Planning date {snapshot.Upcoming.PlanningAsOf:yyyy-MM-dd} • Upcoming window {snapshot.Upcoming.From:yyyy-MM-dd} through {snapshot.Upcoming.Through:yyyy-MM-dd}\n"
            +"Approved cash-method management books. Future commitments are separate from received income. Refresh to capture changes; these figures do not update automatically.");
        panel.Add(Form.Muted($"Financial period {report.From:yyyy-MM-dd} through {report.Through:yyyy-MM-dd}"),DashboardCardsView.Build(snapshot),
            DashboardHighlightsView.Build(snapshot,()=>Open(work),()=>Open(upcoming)),work,upcoming,
            Section("Charts and trends",DashboardChartsView.Build(snapshot)),Section("Financial sources and reports",DashboardFinanceView.Build(snapshot.Finance,navigate)),
            Section("About this dashboard",capture),
            Form.Title("Backup health"),Form.Text(LocalBackupView.HealthText(backup)),Form.Muted("Local archive status is checked separately from these financial figures. Open Backups and recovery for Google connection, Drive and Sheets delivery, and restore-check details."),
            Form.Action("Open backups and recovery",_status,()=>{navigate("Backups and recovery");return Task.CompletedTask;}));
        return panel;
    }

    private static Expander Section(string title,UIElement content)=>new(){Header=title,Content=content,HorizontalAlignment=HorizontalAlignment.Stretch};
    private static void Open(Expander section){section.IsExpanded=true;section.StartBringIntoView();}
}
