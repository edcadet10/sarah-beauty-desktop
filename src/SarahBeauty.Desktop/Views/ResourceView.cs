using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView(BusinessStore store,OwnerSession owner)
{
    private readonly BusinessResources _book=new(store);
    private readonly OperationalResources _operations=new(store);
    private readonly ListView _list=new(){MaxHeight=220};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();
    private readonly ComboBox _kind=Form.Select("Register",["Suppliers","Loans","Assets","Supplies","Actual trips"],"Suppliers");

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Business resources"),Form.Text("These registers describe your suppliers, loans, and assets. Saving a register entry does not post money. Approve actual financial movements in Money."),
            _kind,Form.Action("Show register",_status,()=>{Refresh();return Task.CompletedTask;}),
            Form.Action("New register entry",_status,()=>
            {
                switch(_kind.SelectedItem as string)
                {case "Suppliers":Edit(new Supplier());break;case "Loans":Edit(new Loan());break;case "Assets":Edit(new Asset());break;case "Supplies":Edit(new Supply());break;case "Actual trips":Edit(new MileageTrip());break;}
                return Task.CompletedTask;
            }),_status,_list,_detail);
        _list.SelectionChanged+=(_,_)=>
        {
            try{switch(_list.SelectedItem){case Supplier supplier:Edit(supplier);break;case Loan loan:Edit(loan);break;case Asset asset:Edit(asset);break;case Supply supply:Edit(supply);break;case MileageTrip trip:Edit(trip);break;}}
            catch(Exception error){_status.Text=error.Message;}
        };
        Refresh();
        return panel;
    }

    private void Refresh()
    {
        _list.ItemsSource=(_kind.SelectedItem as string) switch
        {"Loans"=>_book.Loans(owner),"Assets"=>_book.Assets(owner),"Supplies"=>_operations.Supplies(owner),"Actual trips"=>_operations.Trips(owner),_=>_book.Suppliers(owner)};
    }

    private static CheckBox Archived(bool archived)=>new(){Content="Archived (retain history)",IsChecked=archived};
    private static long? OptionalMoney(string value)=>string.IsNullOrWhiteSpace(value)?null:SarahBeauty.Business.Rules.Money.Parse(value);
}
