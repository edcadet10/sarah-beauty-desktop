using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage
{
    private readonly DispatcherTimer _ownerTimer=new(){Interval=TimeSpan.FromSeconds(5)};
    private OwnerActivity? _ownerActivity;
    private bool _stoppingOwnerActivity;
    private void InitializeOwnerActivity()
    {
        AddHandler(UIElement.KeyDownEvent,new KeyEventHandler((_,_)=>RecordOwnerActivity()),true);
        AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,_)=>RecordOwnerActivity()),true);
        AddHandler(UIElement.PointerWheelChangedEvent,new PointerEventHandler((_,_)=>RecordOwnerActivity()),true);
        _ownerTimer.Tick+=(_,_)=>CheckOwnerAccess();Loaded+=(_,_)=>{if(!_stoppingOwnerActivity)_ownerTimer.Start();};Unloaded+=(_,_)=>_ownerTimer.Stop();
    }
    internal void CheckOwnerAccess()
    {
        if(_stoppingOwnerActivity||_session is null)return;
        try{if(_ownerActivity?.Check()!=true)ShowSignIn("Owner access expired or changed. Unsaved fields were cleared. Sign in again.");}
        catch(Exception){OpenCurrentStore();}
    }
    internal void RecordOwnerActivity()
    {
        if(_stoppingOwnerActivity||_session is null)return;
        try{if(_ownerActivity?.RecordInput()!=true)ShowSignIn("Owner access expired or changed. Unsaved fields were cleared. Sign in again.");}
        catch(Exception){OpenCurrentStore();}
    }
    private void LockOwner()
    {
        try{ShowSignIn("Owner access locked. Unsaved fields were cleared. Sign in to continue.");}catch(Exception){OpenCurrentStore();}
    }
    private void HideOwnerContent()
    {
        _workspaceGeneration++;_session?.Lock();_session=null;_ownerActivity=null;_exitReview?.Cancel();Host.Content=null;if(Host.XamlRoot is null)return;
        foreach(var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(Host.XamlRoot).ToArray()){HideDialogs(popup.Child);popup.IsOpen=false;}
    }
    private static void HideDialogs(DependencyObject? root)
    {
        if(root is null)return;var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.TryPop(out var current))
        {
            if(current is ContentDialog dialog){dialog.Hide();continue;}
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(current);i++)pending.Push(VisualTreeHelper.GetChild(current,i));
        }
    }
}
