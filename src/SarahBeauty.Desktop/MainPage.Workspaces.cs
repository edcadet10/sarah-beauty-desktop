using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Storage;
using SarahBeauty.Business.Security;
using SarahBeauty_Desktop.Storage;
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
        HideOwnerContent();var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Title("Open your LIVE business"),Form.Text("No saved LIVE business was found. Create a clean new business, review a complete LIVE recovery archive, or choose TEST for practice."),
            Form.Action("Create a clean LIVE business",status,()=>{ShowLiveCreation();return Task.CompletedTask;}),WorkspaceChoiceAction(status),RecoveryActions(),status);ShowPanel(panel);
    }
    private void ShowLiveCreation()=>ShowBusinessStart(BusinessMode.Live);
    private void EnterAuthenticatedStore(BusinessStore store,OwnerSession owner)
    {
        bool retained=false;
        try
        {
            if(_stoppingOwnerActivity||store.Mode!=_mode)return;var access=new OwnerAccess(store);if(!access.IsSessionCurrent(owner))return;
            StopBackups();HideOwnerContent();_store=store;_access=access;_session=owner;_ownerActivity=new(access,owner);StartBackups();ShowWorkspace();retained=true;
        }
        finally{if(!retained){owner.Lock();if(ReferenceEquals(_session,owner)){_session=null;_ownerActivity=null;}}}
    }
    private void StartBackups()
    {
        _backupRuntime=new(_store!);_googleRuntime=new(_store!);_sheetsRuntime=new(_store!);
    }
}
