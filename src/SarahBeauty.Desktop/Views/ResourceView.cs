using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using System.Text.Json;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView(BusinessStore store,OwnerSession owner)
{
    private readonly BusinessResources _book=new(store);
    private readonly OperationalResources _operations=new(store);
    private readonly ListView _list=new(){MaxHeight=220};
    private readonly StackPanel _panel=Form.Panel(),_detail=Form.Panel();
    private readonly ContentControl _host=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock _status=Form.Text();
    private readonly ComboBox _kind=Form.Select("Register",["Suppliers","Loans","Assets","Supplies","Actual trips"],"Suppliers");
    private string _shownRegister="Suppliers";
    private object? _editing;
    private Func<bool> _hasChanges=()=>false;
    private ContentDialog? _discardDialog;
    private bool _restoringSelection,_saving;

    internal UIElement Build()
    {
        _panel.Add(Form.Title("Business resources"),Form.Text("These registers describe your suppliers, loans, and assets. Saving a register entry does not post money. Approve actual financial movements in Money."),
            _kind,Form.Action("Show register",_status,ShowRegister),Form.Action("New register entry",_status,NewEntry),_status,_list,_detail);
        _list.SelectionChanged+=async(_,_)=>
        {
            try{await SelectEntry();}catch(Exception error){_status.Text=error.Message;}
        };
        _panel.Unloaded+=(_,_)=>_discardDialog?.Hide();
        Refresh();
        _host.Content=_panel;
        return _host;
    }

    private async Task ShowRegister()
    {
        if(!await ConfirmDiscard()){_kind.SelectedItem=_shownRegister;return;}
        ResetRegister();
    }

    private async Task NewEntry()
    {
        if(!await ConfirmDiscard()){_kind.SelectedItem=_shownRegister;return;}
        ResetRegister();
        OpenEntry(_shownRegister switch
        {"Loans"=>new Loan(),"Assets"=>new Asset(),"Supplies"=>new Supply(),"Actual trips"=>new MileageTrip(),_=>new Supplier()});
    }

    private void ResetRegister()
    {
        _shownRegister=(string)_kind.SelectedItem;
        _editing=null;_hasChanges=()=>false;
        _detail.Children.Clear();Refresh();
    }

    private async Task SelectEntry()
    {
        if(_restoringSelection||_list.SelectedItem is not {} value||EntryId(value)==EntryId(_editing))return;
        SelectRow(_editing);
        if(!await ConfirmDiscard())return;
        OpenEntry(value);
    }

    private void OpenEntry(object value)
    {
        switch(value)
        {case Supplier supplier:Edit(supplier);break;case Loan loan:Edit(loan);break;case Asset asset:Edit(asset);break;case Supply supply:Edit(supply);break;case MileageTrip trip:Edit(trip);break;}
    }

    private static string EntryId(object? value)=>value switch
    {Supplier s=>s.Id,Loan l=>l.Id,Asset a=>a.Id,Supply s=>s.Id,MileageTrip t=>t.Id,_=>""};

    private void SelectRow(object? value)
    {
        _restoringSelection=true;
        try{_list.SelectedItem=value is null?null:_list.Items.FirstOrDefault(row=>EntryId(row)==EntryId(value));}
        finally{_restoringSelection=false;}
    }

    private void TrackDraft<T>(T value,Func<T> read)
    {
        string baseline=JsonSerializer.Serialize(read());
        _editing=value;
        _hasChanges=()=>{try{return JsonSerializer.Serialize(read())!=baseline;}catch{return true;}};
        SelectRow(value);
    }

    private async Task<bool> ConfirmDiscard()
    {
        if(_saving||_discardDialog is not null)return false;
        if(!_hasChanges())return true;
        var dialog=new ContentDialog{XamlRoot=_detail.XamlRoot,Title="Discard unsaved register changes?",
            Content="Your register changes have not been saved. Discard them to continue.",PrimaryButtonText="Discard changes",
            CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        _discardDialog=dialog;
        try{return await dialog.ShowAsync()==ContentDialogResult.Primary;}
        finally{_discardDialog=null;}
    }

    private async Task SaveEntry<T>(Func<T> save,Action<T> edit)
    {
        if(_saving||_discardDialog is not null)return;
        _saving=true;_host.IsEnabled=false;
        try{var saved=await Task.Run(save);_kind.SelectedItem=_shownRegister;Refresh();edit(saved);}
        finally{_saving=false;_host.IsEnabled=true;}
    }

    private void Refresh()=>_list.ItemsSource=_shownRegister switch
    {"Loans"=>_book.Loans(owner),"Assets"=>_book.Assets(owner),"Supplies"=>_operations.Supplies(owner),"Actual trips"=>_operations.Trips(owner),_=>_book.Suppliers(owner)};

    private static CheckBox Archived(bool archived)=>new(){Content="Archived (retain history)",IsChecked=archived};
    private static long? OptionalMoney(string value)=>string.IsNullOrWhiteSpace(value)?null:SarahBeauty.Business.Rules.Money.Parse(value);
}
