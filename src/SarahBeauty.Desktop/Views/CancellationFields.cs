using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class CancellationFields
{
    private readonly TextBox _reason,_requested,_date,_agreement,_evidence,_client;
    private readonly ComboBox _terms;
    private readonly CancellationApplications _applications;
    private readonly CancellationCharges _charges;
    private readonly CancellationValues _values;
    internal CancellationFields(CancellationDraft draft,CancellationSnapshot sources,List<Account> accounts,List<Booking> bookings)
    {
        _reason=Form.Input("Cancellation decision and reason",draft.Name);_requested=Form.Input("Cancellation requested date (YYYY-MM-DD)",draft.RequestedDate?.ToString("yyyy-MM-dd")??"");
        _date=Form.Input("Cancellation effective date (YYYY-MM-DD)",draft.Date?.ToString("yyyy-MM-dd")??"");
        _terms=Form.Records("Issued terms reviewed for cancellation",sources.Documents.Where(d=>d.Snapshot.Kind is SarahBeauty.Business.Documents.DocumentKind.Quote or SarahBeauty.Business.Documents.DocumentKind.Invoice).Select(d=>(d.Id,d.ToString())),draft.TermsVersionId,"No issued terms selected");
        _agreement=Form.Input("Actual agreement review",draft.AgreementReview);_evidence=Form.Input("Cancellation agreement evidence",draft.EvidenceId);_client=Form.Input("Client-facing cancellation credit-note reason",draft.ClientReason);
        _applications=new(sources,draft.Unapplications);_charges=new(sources,draft.Reductions);_values=new(sources,accounts,bookings,draft.Values);
    }
    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(_reason,_requested,_date,_terms,_agreement,_evidence,_client,
            Section("Cancellation application reversals",_applications.Build()),Section("Cancellation charge reductions",_charges.Build()),Section("Cancellation value decisions",_values.Build()));return panel;
    }
    internal CancellationDraft Read(CancellationDraft draft)=>draft with{Name=_reason.Text,RequestedDate=Input.Date(_requested.Text),Date=Input.Date(_date.Text),TermsVersionId=_terms.SelectedId(),
        AgreementReview=_agreement.Text,EvidenceId=_evidence.Text,ClientReason=_client.Text,Unapplications=_applications.Read(),Reductions=_charges.Read(),Values=_values.Read()};
    internal bool Changed(CancellationDraft draft){try{return JsonSerializer.Serialize(Read(draft))!=JsonSerializer.Serialize(draft);}catch{return true;}}
    private static Expander Section(string label,UIElement content)=>new(){Header=label,Content=content,HorizontalAlignment=HorizontalAlignment.Stretch};
}
