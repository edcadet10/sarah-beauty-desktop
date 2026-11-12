using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Views;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage
{
    private int _workspaceGeneration;
    private Button WorkspaceChoiceAction(TextBlock status)=>Form.Action("Choose TEST or LIVE",status,()=>{ShowWorkspaceChoices();return Task.CompletedTask;});
    private void ShowWorkspaceChoices()
    {
        HideOwnerContent();StopBackups();_store=null;_access=null;var panel=Form.Panel();var status=Form.Text();var actions=Form.Panel();var choices=new ContentControl{Content=actions};
        actions.Add(Form.Action("Use TEST workspace",status,()=>SelectWorkspace(BusinessMode.Test,choices,status)),Form.Action("Open LIVE workspace",status,()=>SelectWorkspace(BusinessMode.Live,choices,status)));
        panel.Add(Form.Title("Choose your business workspace"),Form.Text("TEST is for practice and fictional records. LIVE keeps the new business in separate files. Choosing another workspace locks owner access; sign in again there."),choices,status);ShowPanel(panel);
    }
    private async Task SelectWorkspace(BusinessMode mode,ContentControl choices,TextBlock status)
    {
        BusinessModes.StoredName(mode);choices.IsEnabled=false;status.Text="Finishing current background work…";
        try{await Task.WhenAll(_stoppingBackups);if(_stoppingOwnerActivity)return;_mode=mode;OpenCurrentStore();}finally{choices.IsEnabled=true;}
    }
    private void ShowMissingLive()
    {
        HideOwnerContent();var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Title("Open your LIVE business"),Form.Text("No saved LIVE business was found. Review and activate a complete LIVE recovery archive, or choose TEST for practice."),WorkspaceChoiceAction(status),RecoveryActions(),status);ShowPanel(panel);
    }
}
