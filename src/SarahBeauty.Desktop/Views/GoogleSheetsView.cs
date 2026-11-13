using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class GoogleSheetsView(BusinessStore store,OwnerSession owner,Func<string>? runtime)
{
    private readonly CheckBox _enabled=new(){Content="Enable readable Google Sheets export"},_automatic=new(){Content="Automatically export pending changes while the app is open"};
    private readonly TextBox _from=Form.Input("Readable financial start (YYYY-MM-DD)"),_through=Form.Input("Readable financial through (YYYY-MM-DD)"),_asOf=Form.Input("Readable planning date (YYYY-MM-DD)"),
        _eventFrom=Form.Input("Readable event start (YYYY-MM-DD)"),_eventThrough=Form.Input("Readable event through (YYYY-MM-DD)"),_interval=Form.Input("Readable export minimum interval in minutes (1–10080)","15");
    private readonly TextBlock _folder=Form.Text(),_health=Form.Text(),_queue=Form.Text(),_runtime=Form.Text(),_status=Form.Text(),_detail=Form.Text();
    private readonly ContentControl _fields=new(),_preview=new();
    private readonly ProgressBar _progress=new(){IsIndeterminate=true,Visibility=Visibility.Collapsed};
    private readonly SearchableRecords _history=new("Readable export job");
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private GoogleOutputPolicy? _policy;
    private GoogleSheetsExportProfile? _profile;
    private IReadOnlyList<GoogleSheetsDeliveryHistory> _jobs=[];
    private CancellationTokenSource? _cancellation;
    private bool _loaded,_initialized;

    internal UIElement Build()
    {
        var dates=Form.Panel();dates.Add(_from,_through,_asOf,_eventFrom,_eventThrough,_automatic,_interval,Form.Text("These dates stay fixed until you change them. They select reports; register and history tables cover the captured business records."),
            Form.Action("Save readable dates and automatic choice",_status,()=>Run(SaveProfile)));
        var fields=Form.Panel();fields.Add(_folder,Form.Text("Creating a new output folder turns both Sheets and archive delivery off until you save their choices again."),
            Form.Action("Create private Google output folder",_status,()=>Run(CreateFolder)),_enabled,Form.Action("Save readable Google Sheets choice",_status,()=>Run(SavePermission)),
            new Expander{Header="Readable report dates and automatic choice",Content=dates,IsExpanded=true},
            Form.Action("Preview readable export locally",_status,()=>Run(Preview)),Form.Action("Export these report dates now",_status,()=>Run(Export)),
            Form.Action("Reload saved readable settings",_status,()=>{Reload();return Task.CompletedTask;}),Form.Action("Refresh readable health and history",_status,()=>{Refresh();return Task.CompletedTask;}),
            _history.Build(),Form.Action("Show readable export attempts",_status,()=>{Show();return Task.CompletedTask;}),Form.Action("Retry selected readable export",_status,()=>Run(Retry)),
            Form.Action("Open last verified workbook",_status,OpenVerified));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Text("Send a private, readable copy of business records and reports. Private notes, sensitivities, credentials, and file binaries are excluded. Correct records in the desktop app; spreadsheet edits are not imported."),
            Form.Text("Sheets delivery needs a verified Google account and private output folder. Automatic export also needs a saved enabled schedule. Work stops when the app is closed or asleep; existing workbooks are retained."),
            _fields,Form.Action("Cancel readable export operation",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_progress,_status,_preview,_health,_queue,_runtime,_detail);
        panel.Loaded+=(_,_)=>{_loaded=true;try{if(!_initialized){Reload();_initialized=true;}else Refresh();}catch(Exception error){_status.Text=error.Message;}_timer.Start();};
        panel.Unloaded+=(_,_)=>{_loaded=false;_timer.Stop();_cancellation?.Cancel();};_timer.Tick+=(_,_)=>ShowRuntime();
        return new Expander{Header="Google Sheets readable export",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private void Reload()
    {
        ReloadPermission();ReloadProfile();_preview.Content=null;Refresh();
    }
    private void ReloadPermission()
    {
        _policy=new GoogleOutputSettings(store).Current(owner);_enabled.IsChecked=_policy?.SheetsExportEnabled??false;
        _folder.Text=_policy is null?"Readable Google folder: Not configured.":$"Readable Google folder: {_policy.FolderName} • Last verified {_policy.FolderVerifiedAtUtc}";
    }
    private void ReloadProfile()
    {
        _profile=new GoogleSheetsExportProfiles(store).Current(owner);_automatic.IsChecked=_profile?.AutomaticEnabled??false;
        DateOnly today=DateOnly.FromDateTime(DateTime.Today);var dates=_profile?.Selection??new(new(today.Year,1,1),today,today,today,today.AddMonths(12));
        _from.Text=Date(dates.FinancialFrom);_through.Text=Date(dates.FinancialThrough);_asOf.Text=Date(dates.PlanningAsOf);_eventFrom.Text=Date(dates.EventFrom);_eventThrough.Text=Date(dates.EventThrough);_interval.Text=(_profile?.MinimumIntervalMinutes??15).ToString(CultureInfo.InvariantCulture);
    }
    private ReadableExportSelection Dates()=>new(Day(_from),Day(_through),Day(_asOf),Day(_eventFrom),Day(_eventThrough));
    private static DateOnly Day(TextBox field)=>DateOnly.TryParseExact(field.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:throw new ArgumentException("Enter readable report dates as YYYY-MM-DD.");
    private static string Date(DateOnly date)=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    private void ShowRuntime()=>_runtime.Text="Readable worker: "+(runtime?.Invoke()??"Unavailable");
}
