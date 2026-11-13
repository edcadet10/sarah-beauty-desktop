using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class TripPaymentView(BusinessStore store,OwnerSession owner,string tripId,Func<bool> tripChanged,Func<bool,Task<bool>> confirmDiscard)
{
    private readonly TripPayments _book=new(store);
    private readonly Dictionary<string,string> _owners=new OwnerAccess(store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
    private readonly TextBox _asOf=Form.Input("Trip reimbursement as-of date (YYYY-MM-DD)",DateTime.Today.ToString("yyyy-MM-dd"));
    private readonly TextBlock _status=Form.Text();
    private readonly StackPanel _result=Form.Panel(),_approval=Form.Panel();
    private readonly ContentControl _host=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private TripPaymentFields? _linkFields;
    private Func<bool> _releaseHasChanges=()=>false;
    private bool _loading;
    private int _version;
    private int _reviewVersion;
    internal bool HasUnsavedChanges=>_linkFields?.HasUnsavedChanges==true||_releaseHasChanges()||_approval.Children.Count>0;

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(_asOf,Form.Action("Load trip reimbursement review",_status,Load),_status,_result);
        _host.Content=panel;
        return new Expander{Header="Trip reimbursement payments",Content=_host,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    private void RequireSavedTrip()
    {
        if(tripChanged())throw new ArgumentException("Save the current trip fields before reviewing or approving its reimbursement links.");
    }

    private async Task Load()
    {
        RequireSavedTrip();
        if(await confirmDiscard(HasUnsavedChanges))await Reload();
    }

    private async Task Reload()
    {
        if(_loading)return;
        _loading=true;_host.IsEnabled=false;
        try{await LoadReview();}finally{_loading=false;_host.IsEnabled=true;}
    }

    private async Task LoadReview()
    {
        RequireSavedTrip();int version=++_version;++_reviewVersion;var date=Input.Date(_asOf.Text)??throw new ArgumentException("Enter the reimbursement review date.");
        var review=await Task.Run(()=>new TripReimbursementSources(store).Load(owner,tripId,date));
        if(version!=_version)return;
        _result.Children.Clear();_approval.Children.Clear();_linkFields=null;_releaseHasChanges=()=>false;var trip=review.CurrentTrip;
        string request=trip.RequestedReimbursementCents.HasValue?Money.Display(trip.RequestedReimbursementCents.Value):"Unknown";
        string remaining=trip.RequestedReimbursementCents.HasValue?Money.Display(trip.RequestedReimbursementCents.Value-review.CurrentClaimLinkedCents):"Unknown";
        _result.Add(Form.Title("Captured trip reimbursement"),Form.Text($"{review.BusinessName} • {review.Mode} • Watermark {review.Watermark}\nGenerated {review.GeneratedAtUtc}\n"
            +$"Saved trip: {trip.Name} • Revision {trip.Revision}\nDriver: {Driver(trip)}\nCurrent reviewed request: {request}\nLinked to this claim through {review.AsOf:yyyy-MM-dd}: {Money.Display(review.CurrentClaimLinkedCents)}\nUnlinked current request: {remaining}"));
        if(review.OtherBasisLinkedCents>0)_result.Add(Form.Text($"Earlier claim bases have {Money.Display(review.OtherBasisLinkedCents)} linked at this date. Their source details retain those earlier reviewed amounts."));
        LinkFields(review,version);History(review);_result.Add(_approval);
    }

    private string Driver(MileageTrip trip)
    {
        if(trip.DriverOwnerId.Length>0)return Actor(trip.DriverOwnerId);
        return new BookingBook(store).Artists(owner).FirstOrDefault(a=>a.Id==trip.DriverArtistId)?.Name??trip.DriverArtistId;
    }

    private string Actor(string id)=>_owners.GetValueOrDefault(id,"Former owner")+" • "+id[..Math.Min(8,id.Length)];

    private void LinkFields(TripReimbursementReview review,int version)
    {
        var fields=new TripPaymentFields(review,new FinanceBook(store).Posted(owner));var input=Form.Panel();var status=Form.Text();
        _linkFields=fields;
        input.Add(fields.Build(),status,Form.Action("Review trip payment link",status,async()=>
        {
            RequireSavedTrip();var draft=fields.Read();int selection=++_reviewVersion;var preview=await Task.Run(()=>_book.Preview(owner,draft));
            if(version!=_version || selection!=_reviewVersion)return;_approval.Children.Clear();string request=Guid.NewGuid().ToString();
            _approval.Add(Form.Title("Review captured trip payment link"),Form.Text($"Saved driver: {Driver(preview.Trip)}\nPayment: {preview.Payment.Source.Name} • {preview.Payment.Source.Date:yyyy-MM-dd}\n"
                +$"Link {Money.Display(draft.Cents)} on {draft.Date:yyyy-MM-dd}\nPayment available before link: {Money.Display(preview.PaymentAvailableCents)}\n"
                +$"Payee review: {draft.PayeeReview}\nEvidence: {draft.EvidenceId}"),
                Form.Action("Edit trip payment link",_status,()=>{++_reviewVersion;_approval.Children.Clear();return Task.CompletedTask;}),
                Approval("Approve trip payment link",async phrase=>
                {
                    if(!await confirmDiscard(_releaseHasChanges()))return;
                    RequireSavedTrip();if(fields.Read()!=draft)throw new ArgumentException("The payment link fields changed. Review them again before approval.");
                    await Task.Run(()=>_book.Approve(owner,draft,preview.Watermark,request,phrase));
                    if(version==_version && selection==_reviewVersion)await Reload();_status.Text="Trip payment linked. Existing books and cash are unchanged.";
                }));
        }));
        _result.Add(new Expander{Header="Link an approved reimbursement payment",Content=input,HorizontalAlignment=HorizontalAlignment.Stretch});
    }

    private UIElement Approval(string label,Func<string,Task> action)
    {
        var panel=Form.Panel();var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase for this trip payment decision");var status=Form.Text();
        panel.Add(phrase,status,Form.Action(label,status,async()=>{string password=phrase.Password;phrase.Password="";await action(password);}));return panel;
    }
}
