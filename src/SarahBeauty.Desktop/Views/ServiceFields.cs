using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class ServiceFields
{
    private readonly TextBox _name,_code,_price,_duration,_evidence;
    private readonly ComboBox _unit,_kind,_tax;
    private readonly CheckBox _archived;
    internal bool IsDirty {get;private set;}

    internal ServiceFields(ServiceDefinition service)
    {
        _name=Form.Input("Service name",service.Name);_code=Form.Input("Permanent service code",service.Code);_code.IsReadOnly=service.Revision>0;
        _price=Form.Input("Catalog unit price in USD",service.UnitCents.HasValue?Money.Input(service.UnitCents.Value):"");
        _duration=Form.Input("Service duration in minutes (blank means unknown)",service.DurationMinutes?.ToString()??"");
        _unit=Form.Select("Catalog unit",["Person","Package","Mile","Custom"],service.Unit);
        _kind=Form.Select("Service meaning",["Service","Trial portion"],service.Kind);
        _tax=Form.Select("Service tax review status",["Unreviewed","Review required","Reviewed"],service.TaxReviewStatus);
        _evidence=Form.Input("Service tax review evidence",service.TaxReviewEvidence);
        _archived=new(){Content="Archive this service",IsChecked=service.Archived};
        foreach(var field in new[]{_name,_code,_price,_duration,_evidence})field.TextChanged+=(_,_)=>IsDirty=true;
        foreach(var field in new[]{_unit,_kind,_tax})field.SelectionChanged+=(_,_)=>IsDirty=true;
        _archived.Checked+=(_,_)=>IsDirty=true;_archived.Unchecked+=(_,_)=>IsDirty=true;
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(_name,_code,_unit,_kind,_price,_duration,_tax,_evidence,_archived,
            Form.Text("The included bridal trial portion is part of its package payment plan. It cannot be added as another billable service. Catalog tax review does not set an actual booking tax rate."));return panel;
    }

    internal ServiceDefinition Read(ServiceDefinition previous)=>previous with{Name=_name.Text,Code=_code.Text,Unit=(string)_unit.SelectedItem,Kind=(string)_kind.SelectedItem,
        UnitCents=string.IsNullOrWhiteSpace(_price.Text)?null:Money.Parse(_price.Text),DurationMinutes=Input.Minutes(_duration.Text),TaxReviewStatus=(string)_tax.SelectedItem,TaxReviewEvidence=_evidence.Text,Archived=_archived.IsChecked==true};
}
